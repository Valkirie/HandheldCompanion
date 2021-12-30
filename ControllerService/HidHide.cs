using ControllerCommon;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Management;
using System.Text.Json;

namespace ControllerService
{
    public class HidHide
    {
        private Process process;
        public RootDevice root;
        public List<Device> devices = new List<Device>();

        private readonly ILogger logger;
        private readonly ControllerService service;

        public HidHide(string _path, ILogger logger, ControllerService service)
        {
            this.logger = logger;
            this.service = service;

            process = new Process
            {
                StartInfo =
                {
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    FileName = _path,
                    Verb = "runas"
                }
            };

            // update and register devices list
            ListDevices();
            RegisterDevices();
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
                string path = Utils.Between(standard_output, "--app-reg \"", "\"");
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

        private void ListDevices()
        {
            process.StartInfo.Arguments = $"--dev-gaming";
            process.Start();
            process.WaitForExit();

            string jsonString = process.StandardOutput.ReadToEnd();

            if (jsonString == "")
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
                string tempString = Utils.Between(jsonString, "symbolicLink", ",");
                root = new RootDevice
                {
                    friendlyName = "Unknown",
                    devices = new List<Device>() { new Device() { gamingDevice = true, deviceInstancePath = tempString } }
                };
            }

            devices = root.devices;
        }

        public void SetCloaking(bool status)
        {
            process.StartInfo.Arguments = status ? $"--cloak-on" : $"--cloak-off";
            process.Start();
            process.WaitForExit();
            process.StandardOutput.ReadToEnd();

            logger.LogInformation("{0} cloak status set to {1}", service.XInputController.instance.ProductName, status);
        }

        public void RegisterDevice(string deviceInstancePath)
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

        private void RegisterDevices()
        {
            foreach (Device d in devices.Where(a => a.gamingDevice))
            {
                string VID = Utils.Between(d.deviceInstancePath.ToLower(), "vid_", "&");
                string PID = Utils.Between(d.deviceInstancePath.ToLower(), "pid_", "&");

                string query = $"SELECT * FROM Win32_PnPEntity WHERE DeviceID LIKE \"%VID_{VID}&PID_{PID}%\"";

                var moSearch = new ManagementObjectSearcher(query);
                var moCollection = moSearch.Get();

                foreach (ManagementObject mo in moCollection)
                {
                    foreach (var item in mo.Properties)
                    {
                        if (item.Name == "DeviceID")
                        {
                            string DeviceID = ((string)item.Value);
                            RegisterDevice(DeviceID);
                            RegisterDevice(d.deviceInstancePath);
                            logger.LogInformation("HideDevice hiding {0}", DeviceID);
                            break;
                        }
                    }
                }
            }
        }
    }
}
