using ControllerCommon.Managers;
using ControllerCommon.Utils;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace ControllerCommon
{
    public class HidHide
    {
        private Process process;

        // The name of the key must include a valid root.
        const string userRoot = @"HKEY_LOCAL_MACHINE";
        const string subkey = @"SOFTWARE\Nefarius Software Solutions e.U.\HidHide";
        const string keyName = userRoot + "\\" + subkey;

        private readonly string key = (string)Registry.GetValue(keyName,
            "Path",
            "");

        public HidHide()
        {
            // verifying HidHide is installed
            string path = null;

            if (key != null)
                path = Path.Combine(key, "x64", "HidHideCLI.exe");

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

        public List<string> GetRegisteredApplications()
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

        public List<string> GetRegisteredDevices()
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

        public void UnregisterApplication(string path)
        {
            process.StartInfo.Arguments = $"--app-unreg \"{path}\"";
            process.Start();
            process.WaitForExit();
            process.StandardOutput.ReadToEnd();
        }

        public void RegisterApplication(string path)
        {
            process.StartInfo.Arguments = $"--app-reg \"{path}\"";
            process.Start();
            process.WaitForExit();
            process.StandardOutput.ReadToEnd();
        }

        /* private void ListDevices()
        {
            process.StartInfo.Arguments = $"--dev-gaming";
            process.Start();
            process.WaitForExit();

            string jsonString = process.StandardOutput.ReadToEnd();

            if (jsonString == "" || jsonString == " [ ] \r\n\r\n")
                return;

            try
            {
                jsonString = jsonString.Replace("\"friendlyName\" : ", "\"friendlyName\" : \"");
                jsonString = jsonString.Replace("[ {", "{");
                jsonString = jsonString.Replace(" } ] } ] ", " } ] }");
                jsonString = jsonString.Replace(@"\", @"\\");
                root = JsonSerializer.Deserialize<RootDevice>(jsonString);
            }
            catch (Exception)
            {
                string tempString = CommonUtils.Between(jsonString, "symbolicLink", ",");
                root = new RootDevice
                {
                    friendlyName = "Unknown",
                    devices = new List<Device>() { new Device() { gamingDevice = true, deviceInstancePath = tempString } }
                };
            }

            devices = root.devices;
        } */

        public void SetCloaking(bool status)
        {
            process.StartInfo.Arguments = status ? $"--cloak-on" : $"--cloak-off";
            process.Start();
            process.WaitForExit();
            process.StandardOutput.ReadToEnd();

            LogManager.LogInformation("Cloak status set to {0}", status);
        }

        public void UnregisterController(string deviceInstancePath)
        {
            LogManager.LogInformation("HideDevice unhiding DeviceID: {0}", deviceInstancePath);
            process.StartInfo.Arguments = $"--dev-unhide \"{deviceInstancePath}\"";
            process.Start();
            process.WaitForExit();
            process.StandardOutput.ReadToEnd();
        }

        public void RegisterController(string deviceInstancePath)
        {
            LogManager.LogInformation("HideDevice hiding DeviceID: {0}", deviceInstancePath);
            process.StartInfo.Arguments = $"--dev-hide \"{deviceInstancePath}\"";
            process.Start();
            process.WaitForExit();
            process.StandardOutput.ReadToEnd();
        }

        public void UnHideDevice(string deviceInstancePath)
        {
            process.StartInfo.Arguments = $"--dev-unhide \"{deviceInstancePath}\"";
            process.Start();
            process.WaitForExit();
            process.StandardOutput.ReadToEnd();
        }
    }
}
