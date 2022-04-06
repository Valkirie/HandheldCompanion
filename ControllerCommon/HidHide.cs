using ControllerCommon.Devices;
using ControllerCommon.Utils;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace ControllerCommon
{
    public class HidHide
    {
        private Process process;

        private readonly ILogger logger;
        private readonly string path = @"C:\Program Files\Nefarius Software Solutions e.U\HidHideCLI\HidHideCLI.exe";

        public HidHide(ILogger logger)
        {
            this.logger = logger;

            // verifying HidHide is installed
            if (!File.Exists(path))
            {
                logger.LogCritical("HidHide is missing. Please get it from: {0}", "https://github.com/ViGEm/HidHide/releases");
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

        public void SetCloaking(bool status, string ProductName)
        {
            process.StartInfo.Arguments = status ? $"--cloak-on" : $"--cloak-off";
            process.Start();
            process.WaitForExit();
            process.StandardOutput.ReadToEnd();

            logger.LogInformation("{0} cloak status set to {1}", ProductName, status);
        }

        private void RegisterController(string deviceInstancePath)
        {
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

        public void RegisterDevice(DeviceController controller)
        {
            RegisterController(controller.DeviceID);
            RegisterController(controller.HID);
            logger.LogInformation("HideDevice hiding DeviceID: {0}", controller.DeviceID);
            logger.LogInformation("HideDevice hiding HID: {0}", controller.HID);
        }
    }
}
