using ControllerCommon.Managers;
using ControllerCommon.Processor;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Printing;
using System.Text;
using System.Threading.Tasks;

namespace ControllerCommon
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
            var pnpResult = StartPnPUtil($"/restart-device \"{InstanceId}\"");
            return ValidateChangeDeviceStatusResult(InstanceId, pnpResult);
        }
        public static bool EnableDevice(string InstanceId) 
        {
            var pnpResult = StartPnPUtil($"/enable-device \"{InstanceId}\"");
            return ValidateChangeDeviceStatusResult(InstanceId, pnpResult);
        }

        public static bool DisableDevice(string InstanceId)
        {
            var pnpResult = StartPnPUtil($"/disable-device \"{InstanceId}\"");
            return ValidateChangeDeviceStatusResult(InstanceId, pnpResult);
        }

        private static PnPUtilResult StartPnPUtil(string arguments)
        {
            using Process process = new();

            process.StartInfo.FileName = "pnputil.exe";
            process.StartInfo.Arguments = arguments;

            process.StartInfo.UseShellExecute = false;
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.RedirectStandardError = true;
            process.StartInfo.CreateNoWindow = true;

            process.Start();

            var output = process.StandardOutput.ReadToEnd();

            process.WaitForExit();

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
