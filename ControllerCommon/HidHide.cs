using ControllerCommon.Managers;
using ControllerCommon.Utils;
using Nefarius.Drivers.HidHide;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace ControllerCommon
{
    public static class HidHide
    {
        private static Process process;
        static HidHide()
        {
            var service = new HidHideControlService();

            // verifying HidHide is installed
            if (!service.IsInstalled)
            {
                LogManager.LogCritical("HidHide is missing. Please get it from: {0}", "https://github.com/ViGEm/HidHide/releases");
                throw new InvalidOperationException();
            }

            // prepare backup path
            string InstallPath = RegistryUtils.GetString(@"SOFTWARE\Nefarius Software Solutions e.U.\HidHide", "Path");
            if (!string.IsNullOrEmpty(InstallPath))
            {
                InstallPath = Path.Combine(InstallPath, "x64", "HidHideCLI.exe");
                if (File.Exists(InstallPath))
                {
                    process = new Process
                    {
                        StartInfo =
                        {
                            UseShellExecute = false,
                            RedirectStandardOutput = true,
                            RedirectStandardError = true,
                            CreateNoWindow = true,
                            FileName = InstallPath,
                            Verb = "runas"
                        }
                    };
                }
            }
        }

        public static List<string> GetRegisteredApplications()
        {
            try
            {
                var service = new HidHideControlService();
                return service.ApplicationPaths.ToList();
            }
            catch { }

            return new();
        }

        public static List<string> GetRegisteredDevices()
        {
            try
            {
                var service = new HidHideControlService();
                return service.BlockedInstanceIds.ToList();
            }
            catch { }

            return new();
        }

        public static bool IsRegistered(string InstanceId)
        {
            try
            {
                var registered = GetRegisteredDevices();
                return registered.Contains(InstanceId);
            }
            catch { }

            return false;
        }

        public static void UnregisterApplication(string fileName)
        {
            try
            {
                var service = new HidHideControlService();
                if (service.ApplicationPaths.Contains(fileName))
                {
                    service.RemoveApplicationPath(fileName);
                    LogManager.LogInformation("HideDevice RemoveApplicationPath: {0}", fileName);
                }
            }
            catch
            {
                if (process is null)
                    return;

                process.StartInfo.Arguments = $"--app-unreg \"{fileName}\"";
                process.Start();
                process.WaitForExit();
                process.StandardOutput.ReadToEnd();

                LogManager.LogInformation("HideDevice RemoveApplicationPath: {0}", fileName);
            }
        }

        public static void RegisterApplication(string fileName)
        {
            try
            {
                var service = new HidHideControlService();
                if (!service.ApplicationPaths.Contains(fileName))
                {
                    service.AddApplicationPath(fileName);
                    LogManager.LogInformation("HideDevice AddApplicationPath: {0}", fileName);
                }
            }
            catch
            {
                if (process is null)
                    return;

                process.StartInfo.Arguments = $"--app-reg \"{fileName}\"";
                process.Start();
                process.WaitForExit();
                process.StandardOutput.ReadToEnd();

                LogManager.LogInformation("HideDevice AddApplicationPath: {0}", fileName);
            }
        }

        public static void SetCloaking(bool status)
        {
            try
            {
                var service = new HidHideControlService();
                service.IsActive = status;
                LogManager.LogInformation("HideDevice IsActive: {0}", status);
            }
            catch { }
        }

        public static void UnhidePath(string deviceInstancePath)
        {
            try
            {
                var service = new HidHideControlService();
                if (service.BlockedInstanceIds.Contains(deviceInstancePath))
                {
                    service.RemoveBlockedInstanceId(deviceInstancePath);
                    LogManager.LogInformation("HideDevice RemoveBlockedInstanceId: {0}", deviceInstancePath);
                }
            }
            catch { }
        }

        public static void HidePath(string deviceInstancePath)
        {
            try
            {
                var service = new HidHideControlService();
                if (!service.BlockedInstanceIds.Contains(deviceInstancePath))
                {
                    service.AddBlockedInstanceId(deviceInstancePath);
                    LogManager.LogInformation("HideDevice AddBlockedInstanceId: {0}", deviceInstancePath);
                }
            }
            catch { }
        }
    }
}