<<<<<<< HEAD
﻿using System.Collections.Generic;
using System.Diagnostics;
using System.Text.RegularExpressions;
=======
﻿using System.Diagnostics;
>>>>>>> f8fea3c25fb5fd254f5020d43305b7356ec9770d

namespace HandheldCompanion
{
    // https://learn.microsoft.com/en-us/windows-hardware/drivers/devtest/pnputil-return-values
    public class PnPUtilResult
    {
        public int ExitCode;
        public string StandardOutput;

        public PnPUtilResult(int exitCode, string output)
        {
            ExitCode = exitCode;
            StandardOutput = output;
        }
    }

    public static class PnPUtil
    {
        private const int ERROR_SUCCESS = 0;
        private const int ERROR_NO_MORE_ITEMS = 259;
        private const int ERROR_SUCCESS_REBOOT_REQUIRED = 3010;
        private const int ERROR_SUCCESS_REBOOT_INITIATED = 1641;

        public static bool RestartDevice(string InstanceId)
        {
<<<<<<< HEAD
            var pnpResult = GetPnPUtilResult($"/restart-device \"{InstanceId}\"");
            return ValidateChangeDeviceStatusResult(InstanceId, pnpResult);
        }

        public static bool EnableDevice(string InstanceId)
        {
            var pnpResult = GetPnPUtilResult($"/enable-device \"{InstanceId}\"");
=======
            var pnpResult = StartPnPUtil($"/restart-device \"{InstanceId}\"");
            return ValidateChangeDeviceStatusResult(InstanceId, pnpResult);
        }
        public static bool EnableDevice(string InstanceId)
        {
            var pnpResult = StartPnPUtil($"/enable-device \"{InstanceId}\"");
>>>>>>> f8fea3c25fb5fd254f5020d43305b7356ec9770d
            return ValidateChangeDeviceStatusResult(InstanceId, pnpResult);
        }

        public static bool DisableDevice(string InstanceId)
        {
<<<<<<< HEAD
            var pnpResult = GetPnPUtilResult($"/disable-device \"{InstanceId}\"");
            return ValidateChangeDeviceStatusResult(InstanceId, pnpResult);
        }

        public static bool EnableDevices(string Class)
        {
            var pnpResult = GetPnPUtilResult($"/enable-device /class \"{Class}\"");
            return pnpResult.ExitCode == ERROR_SUCCESS;
        }

        public static List<string> GetDevices(string className, string status = "/connected")
        {
            // A list of string to store the Instance ID values
            List<string> instanceIDs = new List<string>();

            // A regular expression to match the Instance ID pattern
            Regex regex = new Regex(@"Instance ID:\s+(.*)");

            // Loop through each line of the input string
            string input = GetPnPUtilOutput($"/enum-devices {status} /class {className}");
            foreach (string line in input.Split('\r'))
            {
                // Try to match the line with the regular expression
                Match match = regex.Match(line);

                // If there is a match, add the Instance ID value to the list
                if (match.Success)
                {
                    instanceIDs.Add(match.Groups[1].Value);
                }
            }

            // Print the list of Instance ID values
            Debug.WriteLine("The Instance ID values are:");
            foreach (string id in instanceIDs)
            {
                Debug.WriteLine(id);
            }

            return instanceIDs;
        }

        public static Process StartPnPUtil(string arguments)
        {
            Process process = new();
=======
            var pnpResult = StartPnPUtil($"/disable-device \"{InstanceId}\"");
            return ValidateChangeDeviceStatusResult(InstanceId, pnpResult);
        }

        private static PnPUtilResult StartPnPUtil(string arguments)
        {
            using Process process = new();
>>>>>>> f8fea3c25fb5fd254f5020d43305b7356ec9770d

            process.StartInfo.FileName = "pnputil.exe";
            process.StartInfo.Arguments = arguments;

            process.StartInfo.UseShellExecute = false;
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.RedirectStandardError = true;
            process.StartInfo.CreateNoWindow = true;

            process.Start();

<<<<<<< HEAD
            return process;
        }

        private static string GetPnPUtilOutput(string arguments)
        {
            Process process = StartPnPUtil(arguments);
            var output = process.StandardOutput.ReadToEnd();

            return output;
        }

        private static PnPUtilResult GetPnPUtilResult(string arguments)
        {
            Process process = StartPnPUtil(arguments);
            var output = process.StandardOutput.ReadToEnd();
=======
            var output = process.StandardOutput.ReadToEnd();

            process.WaitForExit();

>>>>>>> f8fea3c25fb5fd254f5020d43305b7356ec9770d
            var exitCode = process.ExitCode;

            return new PnPUtilResult(exitCode, output);
        }

        // this function validates the results for /enable-device, /disable-device and /restart-device.
        private static bool ValidateChangeDeviceStatusResult(string instanceId, PnPUtilResult pnpResult)
        {
            string[] output = pnpResult.StandardOutput.Split("\r\n");
            var exitCode = pnpResult.ExitCode;

            switch (exitCode)
            {
                case ERROR_SUCCESS:
                    if (output[2].Contains(instanceId))
                        // we assume the operation was successful if the instance id was found
                        return true;
                    break;
                default:
                    // operation was not successful or requires a reboot.
                    return false;
            }

            return true;
        }
    }
}
