using HandheldCompanion.Extensions;
using HandheldCompanion.Shared;
using HandheldCompanion.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Management;
using System.Threading.Tasks;

namespace HandheldCompanion
{
    public static class WMI
    {
        public static async Task<bool> ExistsAsync(string scope, FormattableString query)
        {
            try
            {
                var queryFormatted = query.ToString(WMIPropertyValueFormatter.Instance);
                var mos = new ManagementObjectSearcher(scope, queryFormatted);
                var managementObjects = await mos.GetAsync().ConfigureAwait(false);
                return managementObjects.Any();
            }
            catch
            {
                return false;
            }
        }

        public static IDisposable Listen(string scope, FormattableString query, Action<PropertyDataCollection> handler)
        {
            var queryFormatted = query.ToString(WMIPropertyValueFormatter.Instance);
            var watcher = new ManagementEventWatcher(scope, queryFormatted);
            watcher.EventArrived += (_, e) => handler(e.NewEvent.Properties);
            watcher.Start();

            return new LambdaDisposable(() =>
            {
                watcher.Stop();
                watcher.Dispose();
            });
        }

        public static async Task<IEnumerable<T>> ReadAsync<T>(string scope, FormattableString query, Func<PropertyDataCollection, T> converter)
        {
            try
            {
                var queryFormatted = query.ToString(WMIPropertyValueFormatter.Instance);
                var mos = new ManagementObjectSearcher(scope, queryFormatted);
                var managementObjects = await mos.GetAsync().ConfigureAwait(false);
                var result = managementObjects.Select(mo => mo.Properties).Select(converter);
                return result;
            }
            catch (ManagementException ex)
            {
                LogManager.LogError($"Read failed: {ex.Message}. [scope={scope}, query={query}]", ex);
                return Enumerable.Empty<T>();
            }
        }

        public static void Set(string scope, string path, string methodName, byte[] fullPackage)
        {
            // Create the management object using the provided scope and path
            ManagementObject managementObject = new ManagementObject(scope, path, null);

            ManagementBaseObject inParams = null;
            ManagementBaseObject inParamsData = null;
            bool parametersAvailable = false;

            // First attempt: retrieve method parameters for specified methodName
            try
            {
                inParams = managementObject.GetMethodParameters(methodName);
                inParamsData = inParams["Data"] as ManagementBaseObject;
                parametersAvailable = (inParams != null && inParamsData != null);
            }
            catch (Exception ex) { }

            // If the "Data" parameter was not obtained, try the fallback method "Get_WMI"
            if (!parametersAvailable)
            {
                try
                {
                    inParams = managementObject.InvokeMethod("Get_WMI", null, null);
                    inParamsData = inParams["Data"] as ManagementBaseObject;
                }
                catch (ManagementException mex) { }
                catch (Exception ex) { }
            }

            // If we still don't have valid input parameters, throw an exception
            if (inParams == null || inParamsData == null)
            {
                LogManager.LogError("WMI Call failed: [scope={0}, path={1}, methodName={2}, fullPackage={3}]", scope, path, methodName, string.Join(',', fullPackage));
                return;
            }

            // Set the "Bytes" property of the "Data" parameter to the full package
            inParamsData.SetPropertyValue("Bytes", fullPackage);
            inParams.SetPropertyValue("Data", inParamsData);

            // Invoke the method with the parameters
            managementObject.InvokeMethod(methodName, inParams, null);
        }

        public static byte[] Get(string scope, string path, string methodName, byte? command)
        {
            // Connect to the WMI scope.
            ManagementScope managementScope = new ManagementScope(scope);
            managementScope.Connect();

            // Create a ManagementPath for the WMI class.
            ManagementPath managementPath = new ManagementPath(path);

            using (ManagementClass managementClass = new ManagementClass(managementScope, managementPath, null))
            {
                ManagementBaseObject inParams = null;
                if (command.HasValue)
                {
                    // Retrieve the method's input parameter template.
                    inParams = managementClass.GetMethodParameters(methodName);

                    // Create a byte array of the assumed size and set the first byte to the command value.
                    byte[] dataBytes = new byte[32];
                    dataBytes[0] = command.Value;

                    // The decompiled code expects the "Data" property to be a nested object with a "Bytes" property.
                    // Try to use the provided embedded object, if available.
                    ManagementBaseObject dataObject = inParams["Data"] as ManagementBaseObject;
                    if (dataObject != null)
                    {
                        dataObject["Bytes"] = dataBytes;
                        inParams["Data"] = dataObject;
                    }
                    else
                    {
                        // If no embedded object exists, assign the byte array directly.
                        inParams["Data"] = dataBytes;
                    }
                }

                // Invoke the method with the prepared input parameters (or null if no command was provided).
                ManagementBaseObject outParams = managementClass.InvokeMethod(methodName, inParams, null);

                // Extract and return the byte array from the output parameters.
                if (outParams != null)
                {
                    object dataField = outParams["Data"];
                    if (dataField is ManagementBaseObject mbo)
                    {
                        object bytesField = mbo["Bytes"];
                        if (bytesField is byte[] bytes)
                        {
                            return bytes;
                        }
                    }
                    else if (dataField is byte[] bytes)
                    {
                        return bytes;
                    }
                }
            }
            return null;
        }

        public static async Task CallAsync(string scope, FormattableString query, string methodName, Dictionary<string, object> methodParams)
        {
            try
            {
                var queryFormatted = query.ToString(WMIPropertyValueFormatter.Instance);
                var mos = new ManagementObjectSearcher(scope, queryFormatted);
                var managementObjects = await mos.GetAsync().ConfigureAwait(false);
                var managementObject = managementObjects.FirstOrDefault();

                // Check if managementObject is null and return the default value
                if (managementObject == null)
                    return;

                var mo = (ManagementObject)managementObject;
                var methodParamsObject = mo.GetMethodParameters(methodName);
                foreach (var pair in methodParams)
                    methodParamsObject[pair.Key] = pair.Value;

                mo.InvokeMethod(methodName, methodParamsObject, new InvokeMethodOptions());
            }
            catch (ManagementException ex)
            {
                LogManager.LogError($"Call failed: {ex.Message}. [scope={scope}, query={query}, methodName={methodName}]", ex);
            }
        }

        public static async Task<T> CallAsync<T>(string scope, FormattableString query, string methodName, Dictionary<string, object> methodParams, Func<PropertyDataCollection, T> converter)
        {
            // Define a default value for the type T
            T defaultValue = default(T);

            try
            {
                var queryFormatted = query.ToString(WMIPropertyValueFormatter.Instance);

                var mos = new ManagementObjectSearcher(scope, queryFormatted);
                var managementObjects = await mos.GetAsync().ConfigureAwait(false);
                var managementObject = managementObjects.FirstOrDefault();

                // Check if managementObject is null and return the default value
                if (managementObject == null)
                    return defaultValue;

                var mo = (ManagementObject)managementObject;
                var methodParamsObject = mo.GetMethodParameters(methodName);
                foreach (var pair in methodParams)
                    methodParamsObject[pair.Key] = pair.Value;

                var resultProperties = mo.InvokeMethod(methodName, methodParamsObject, new InvokeMethodOptions());
                var result = converter(resultProperties.Properties);
                return result;
            }
            catch (ManagementException ex)
            {
                // Log the exception details and return the default value
                LogManager.LogError($"Call failed: {ex.Message}. [scope={scope}, query={query}, methodName={methodName}]");
                return defaultValue;
            }
        }

        public class WMIPropertyValueFormatter : IFormatProvider, ICustomFormatter
        {
            public static readonly WMIPropertyValueFormatter Instance = new();

            private WMIPropertyValueFormatter() { }

            public object GetFormat(Type? formatType)
            {
                if (formatType == typeof(ICustomFormatter))
                    return this;

                throw new InvalidOperationException("Invalid type of formatted");
            }

            public string Format(string? format, object? arg, IFormatProvider? formatProvider)
            {
                var stringArg = arg?.ToString()?.Replace("\\", "\\\\");
                return stringArg ?? string.Empty;
            }
        }
    }
}
