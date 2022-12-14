using ControllerCommon.Managers;
using Nefarius.Drivers.HidHide;
using System;
using System.Collections.Generic;

namespace ControllerCommon
{
    public static class HidHide
    {
        static HidHide()
        {
            var service = new HidHideControlService();

            if (!service.IsInstalled)
            {
                LogManager.LogCritical("HidHide is missing. Please get it from: {0}", "https://github.com/ViGEm/HidHide/releases");
                throw new InvalidOperationException();
            }
        }

        public static IReadOnlyList<string> GetRegisteredApplications()
        {
            var service = new HidHideControlService();
            return service.ApplicationPaths;
        }

        public static IReadOnlyList<string> GetRegisteredDevices()
        {
            var service = new HidHideControlService();
            return service.BlockedInstanceIds;
        }

        public static void UnregisterApplication(string fileName)
        {
            try
            {
                var service = new HidHideControlService();
                service.RemoveApplicationPath(fileName);
                LogManager.LogInformation("HideDevice RemoveApplicationPath: {0}", fileName);
            }
            catch { }
        }

        public static void RegisterApplication(string fileName)
        {
            try
            {
                var service = new HidHideControlService();
                service.AddApplicationPath(fileName);
                LogManager.LogInformation("HideDevice AddApplicationPath: {0}", fileName);
            }
            catch { }
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
                service.RemoveBlockedInstanceId(deviceInstancePath);
                LogManager.LogInformation("HideDevice RemoveBlockedInstanceId: {0}", deviceInstancePath);
            }
            catch { }
        }

        public static void HidePath(string deviceInstancePath)
        {
            try
            {
                var service = new HidHideControlService();
                service.AddBlockedInstanceId(deviceInstancePath);
                LogManager.LogInformation("HideDevice AddBlockedInstanceId: {0}", deviceInstancePath);
            }
            catch { }
        }
    }
}
