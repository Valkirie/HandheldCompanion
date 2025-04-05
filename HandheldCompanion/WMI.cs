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

        public static int GetAPLength(byte iDataBlockIndex)
        {
            /*
             * Get_AP
             * switch (iDataBlockIndex)
             * case 0: length = 6;
             * case 1: length = 3;
             * case 2: length = 7;
             */
            switch (iDataBlockIndex)
            {
                case 0:
                    return 6;
                case 1:
                    return 3;
                case 2:
                    return 7;
                default:
                    return 32;
            }
        }

        public static ManagementBaseObject Set(string scope, string path, string methodName, byte[] fullPackage)
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
                return null;
            }

            // Set the "Bytes" property of the "Data" parameter to the full package
            inParamsData.SetPropertyValue("Bytes", fullPackage);
            inParams.SetPropertyValue("Data", inParamsData);

            // Invoke the method with the parameters
            return managementObject.InvokeMethod(methodName, inParams, null);
        }

        public static byte[] Get(string scope, string path, string methodName, byte iDataBlockIndex, int length, out bool readSuccess)
        {
            byte[] fullPackage = new byte[32];
            byte[] resultData = new byte[length];

            fullPackage[0] = iDataBlockIndex;
            readSuccess = false;

            // Extract the output bytes from the nested 'Data' object.
            ManagementBaseObject outParams = Set(scope, path, methodName, fullPackage);
            if (outParams == null)
                return resultData;

            ManagementBaseObject dataOut = outParams["Data"] as ManagementBaseObject;
            if (dataOut == null)
            {
                LogManager.LogError("WMI Call failed at outParams[\"Data\"]: [scope={0}, path={1}, methodName={2}, iDataBlockIndex={3}, length={4}]", scope, path, methodName, iDataBlockIndex, length);
                return resultData;
            }

            byte[] outBytes = dataOut["Bytes"] as byte[];
            if (outBytes == null || outBytes.Length < 1)
            {
                LogManager.LogError("WMI Call failed at dataOut[\"Bytes\"]: [scope={0}, path={1}, methodName={2}, iDataBlockIndex={3}, length={4}]", scope, path, methodName, iDataBlockIndex, length);
                return resultData;
            }

            // The first byte is the flag; subsequent bytes contain the actual data.
            byte flag = outBytes[0];
            readSuccess = (flag == 1);

            // Copy the remaining bytes as the returned data.
            int dataLength = outBytes.Length - 1;
            resultData = new byte[dataLength];
            Array.Copy(outBytes, 1, resultData, 0, dataLength);

            return resultData;
        }

        public static void Call(string scope, string query, string methodName, Dictionary<string, object> methodParams)
        {
            using var searcher = new ManagementObjectSearcher(scope, query);
            var managementObject = searcher.Get().Cast<ManagementObject>().FirstOrDefault();

            if (managementObject == null)
                return;

            using var methodParamsObject = managementObject.GetMethodParameters(methodName);
            foreach (var pair in methodParams)
                methodParamsObject[pair.Key] = pair.Value;

            managementObject.InvokeMethod(methodName, methodParamsObject, null);
        }

        public static T Call<T>(string scope, string query, string methodName, Dictionary<string, object> methodParams, Func<PropertyDataCollection, T> resultSelector)
        {
            using var searcher = new ManagementObjectSearcher(scope, query);
            var managementObject = searcher.Get().Cast<ManagementObject>().FirstOrDefault();

            if (managementObject == null)
                return default;

            using var methodParamsObject = managementObject.GetMethodParameters(methodName);
            foreach (var pair in methodParams)
                methodParamsObject[pair.Key] = pair.Value;

            var result = managementObject.InvokeMethod(methodName, methodParamsObject, null);
            return resultSelector(result.Properties);
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
