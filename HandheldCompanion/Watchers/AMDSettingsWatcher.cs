using HandheldCompanion.GraphicsProcessingUnit;
using HandheldCompanion.Managers;
using HandheldCompanion.Notifications;
using HandheldCompanion.Utils;
using Microsoft.Win32;
using System;
using System.Linq;

namespace HandheldCompanion.Watchers
{
    public class AMDSettingsWatcher : ISpaceWatcher
    {
        // Display adapter class GUID
        private const string DisplayClassKeyPath = @"SYSTEM\CurrentControlSet\Control\Class\{4d36e968-e325-11ce-bfc1-08002be10318}";

        private readonly AMDIntegerScalingNotification scalingNotification = new();

        public AMDSettingsWatcher() { }

        public override void Start()
        {
            ManagerFactory.gpuManager.Hooked += GpuManager_Hooked;
            base.Start();
        }

        public override void Stop()
        {
            ManagerFactory.gpuManager.Hooked -= GpuManager_Hooked;
            base.Stop();
        }

        private void GpuManager_Hooked(GPU gpu)
        {
            if (gpu is not AMDGPU)
                return;

            string gpuName = gpu.ToString();
            if (!TryResolveAdapterRegistryPath(gpuName, out string adapterKeyPath))
            {
                // If we cannot map the GPU name to a registry key, do not spam the user.
                // You may want to LogManager.LogWarning here if your project has it.
                ManagerFactory.notificationManager.Discard(scalingNotification);
                return;
            }

            int embeddedSupport = RegistryUtils.GetInt(adapterKeyPath, "DalEmbeddedIntegerScalingSupport");
            if (embeddedSupport == 1)
            {
                ManagerFactory.notificationManager.Discard(scalingNotification);
            }
            else
            {
                // Update target key so the "Fix" action writes to the correct adapter.
                scalingNotification.SetTargetRegistryPath(adapterKeyPath);
                ManagerFactory.notificationManager.Add(scalingNotification);
            }
        }

        private static bool TryResolveAdapterRegistryPath(string gpuName, out string adapterKeyPath)
        {
            // set value
            adapterKeyPath = string.Empty;

            using RegistryKey classKey = Registry.LocalMachine.OpenSubKey(DisplayClassKeyPath, writable: false);
            if (classKey is null)
                return false;

            // Subkeys are typically 0000, 0001, ...
            foreach (string subName in classKey.GetSubKeyNames().Where(n => n.Length == 4 && n.All(char.IsDigit)))
            {
                using RegistryKey adapterKey = classKey.OpenSubKey(subName, writable: false);
                if (adapterKey is null)
                    continue;

                string driverDesc = adapterKey.GetValue("DriverDesc") as string;
                if (string.IsNullOrEmpty(driverDesc))
                    continue;

                if (string.Equals(driverDesc, gpuName, StringComparison.OrdinalIgnoreCase))
                {
                    adapterKeyPath = $@"{DisplayClassKeyPath}\{subName}";
                    return true;
                }
            }

            return false;
        }
    }
}
