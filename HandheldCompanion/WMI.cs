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

        public static byte[] Get(string scope, string path, string methodName, ref bool success)
        {
            // Create the management object using the provided scope and path
            ManagementObject managementObject = new ManagementObject(scope, path, null);

            ManagementBaseObject inParams = null;
            ManagementBaseObject inParamsData = null;
            bool parametersAvailable = false;

            // First attempt: retrieve method parameters for the specified methodName
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

            // If we still don't have valid input parameters, log an error and return null
            if (inParams == null || inParamsData == null)
            {
                LogManager.LogError("WMI CallGet failed: [scope={0}, path={1}, methodName={2}]", scope, path, methodName);
                success = false;
                return null;
            }

            // Invoke the method with the parameters (here we are retrieving data, so no setting is needed)
            ManagementBaseObject outParams = managementObject.InvokeMethod(methodName, inParams, null);

            // Retrieve the "Data" object from the output parameters
            ManagementBaseObject outParamsData = outParams["Data"] as ManagementBaseObject;
            if (outParamsData == null)
            {
                LogManager.LogError("WMI CallGet failed to retrieve data object: [scope={0}, path={1}, methodName={2}]", scope, path, methodName);
                success = false;
                return null;
            }

            // Extract and return the "Bytes" property from the Data object
            byte[] fullPackage = outParamsData["Bytes"] as byte[];
            success = true;
            return fullPackage;
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
