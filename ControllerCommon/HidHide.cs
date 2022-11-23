using ControllerCommon.Managers;
using ControllerCommon.Utils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace ControllerCommon
{
    public static class HidHide
    {
        private static Process process;

        static HidHide()
        {
            // verifying HidHide is installed
            string path = RegistryUtils.GetHKLM(@"SOFTWARE\Nefarius Software Solutions e.U.\HidHide", "Path");
            if (!string.IsNullOrEmpty(path))
                path = Path.Combine(path, "x64", "HidHideCLI.exe");

            if (!File.Exists(path))
            {
                LogManager.LogWarning("HidHide is missing. Application behavior will be degraded. Please get it from: {0}", "https://github.com/ViGEm/HidHide/releases");
                throw new InvalidOperationException();
            }

            process = new Process
            {
                StartInfo =
                {
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    FileName = path,
                    Verb = "runas"
                }
            };
        }

        public static List<string> GetRegisteredApplications()
        {
            process.StartInfo.Arguments = $"--app-list";
            process.Start();
            process.WaitForExit();

            string standard_output;
            List<string> whitelist = new List<string>();
            while ((standard_output = process.StandardOutput.ReadLine()) != null)
            {
                if (!standard_output.Contains("app-reg"))
                    break;

                // --app-reg \"C:\\Program Files\\Nefarius Software Solutions e.U\\HidHideCLI\\HidHideCLI.exe\"
                string path = CommonUtils.Between(standard_output, "--app-reg \"", "\"");
                whitelist.Add(path);
            }
            return whitelist;
        }

        public static List<string> GetRegisteredDevices()
        {
            process.StartInfo.Arguments = $"--dev-list";
            process.Start();
            process.WaitForExit();

            string standard_output;
            List<string> devices = new List<string>();
            while ((standard_output = process.StandardOutput.ReadLine()) != null)
            {
                if (!standard_output.Contains("dev-hide"))
                    break;

                // --app-reg \"C:\\Program Files\\Nefarius Software Solutions e.U\\HidHideCLI\\HidHideCLI.exe\"
                string path = CommonUtils.Between(standard_output, "--dev-hide \"", "\"");
                devices.Add(path);
            }
            return devices;
        }

        public static void UnregisterApplication(string path)
        {
            process.StartInfo.Arguments = $"--app-unreg \"{path}\"";
            process.Start();
            process.WaitForExit();
            process.StandardOutput.ReadToEnd();
        }

        public static void RegisterApplication(string path)
        {
            process.StartInfo.Arguments = $"--app-reg \"{path}\"";
            process.Start();
            process.WaitForExit();
            process.StandardOutput.ReadToEnd();
        }

        public static void SetCloaking(bool status)
        {
            process.StartInfo.Arguments = status ? $"--cloak-on" : $"--cloak-off";
            process.Start();
            process.WaitForExit();
            process.StandardOutput.ReadToEnd();

            LogManager.LogInformation("Cloak status set to {0}", status);
        }

        public static void UnhidePath(string deviceInstancePath)
        {
            LogManager.LogInformation("HideDevice unhiding DeviceID: {0}", deviceInstancePath);
            process.StartInfo.Arguments = $"--dev-unhide \"{deviceInstancePath}\"";
            process.Start();
            process.WaitForExit();
            process.StandardOutput.ReadToEnd();
        }

        public static void HidePath(string deviceInstancePath)
        {
            LogManager.LogInformation("HideDevice hiding DeviceID: {0}", deviceInstancePath);
            process.StartInfo.Arguments = $"--dev-hide \"{deviceInstancePath}\"";
            process.Start();
            process.WaitForExit();
            process.StandardOutput.ReadToEnd();
        }
    }
}
