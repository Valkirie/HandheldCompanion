using HandheldCompanion.Managers;
using HandheldCompanion.Utils;
using HandheldCompanion.Views;
using Nefarius.Drivers.HidHide;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;

namespace HandheldCompanion;

public static class HidHide
{
    private static readonly Process process;

    static HidHide()
    {
        var service = new HidHideControlService();

        // verifying HidHide is installed
        if (!service.IsInstalled)
        {
            LogManager.LogCritical("HidHide is missing. Please get it from: {0}", "https://github.com/ViGEm/HidHide/releases");

            MainWindow.SplashScreen.Close();
            MessageBox.Show("Unable to start Handheld Companion, the HidHide application is missing.\n\nPlease get it from: https://github.com/ViGEm/HidHide/releases", "Error");
            throw new InvalidOperationException();
        }

        // prepare backup path
        var InstallPath = RegistryUtils.GetString(@"SOFTWARE\Nefarius Software Solutions e.U.\HidHide", "Path");
        if (!string.IsNullOrEmpty(InstallPath))
        {
            InstallPath = Path.Combine(InstallPath, "x64", "HidHideCLI.exe");
            if (File.Exists(InstallPath))
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

    public static List<string> GetRegisteredApplications()
    {
        try
        {
            var service = new HidHideControlService();
            return service.ApplicationPaths.ToList();
        }
        catch
        {
        }

        return [];
    }

    public static List<string> GetRegisteredDevices()
    {
        try
        {
            var service = new HidHideControlService();
            return service.BlockedInstanceIds.Select(x => x.ToUpper()).ToList();
        }
        catch
        {
        }

        return [];
    }

    public static bool IsRegistered(string InstanceId)
    {
        try
        {
            var registered = GetRegisteredDevices();
            return registered.Contains(InstanceId.ToUpper());
        }
        catch
        {
        }

        return false;
    }

    public static bool UnregisterApplication(string fileName)
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
            LogManager.LogError("Failed to UnregisterApplication({0}), HidHideControlService is unreachable", fileName);

            if (process is null)
                return false;

            process.StartInfo.Arguments = $"--app-unreg \"{fileName}\"";
            process.Start();
            bool success = process.WaitForExit(TimeSpan.FromSeconds(3));

            if (success)
            {
                process.StandardOutput.ReadToEnd(); // todo: parse result
                LogManager.LogInformation("HideDevice RemoveApplicationPath: {0}", fileName);
            }
        }

        return true;
    }

    public static bool RegisterApplication(string fileName)
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
            LogManager.LogError("Failed to RegisterApplication({0}), HidHideControlService is unreachable", fileName);

            if (process is null)
                return false;

            process.StartInfo.Arguments = $"--app-reg \"{fileName}\"";
            process.Start();
            bool success = process.WaitForExit(TimeSpan.FromSeconds(3));

            if (success)
            {
                process.StandardOutput.ReadToEnd(); // todo: parse result
                LogManager.LogInformation("HideDevice AddApplicationPath: {0}", fileName);
            }
        }

        return true;
    }

    public static bool SetCloaking(bool status)
    {
        try
        {
            var service = new HidHideControlService
            {
                IsActive = status
            };
            LogManager.LogInformation("HideDevice IsActive: {0}", status);
        }
        catch
        {
            LogManager.LogError("Failed to SetCloaking({0}), HidHideControlService is unreachable", status);

            if (process is null)
                return false;

            switch (status)
            {
                case true:
                    process.StartInfo.Arguments = $"--cloak-on";
                    break;
                case false:
                    process.StartInfo.Arguments = $"--cloak-off";
                    break;
            }
            process.Start();
            bool success = process.WaitForExit(TimeSpan.FromSeconds(3));

            if (success)
            {
                process.StandardOutput.ReadToEnd(); // todo: parse result
                LogManager.LogInformation("HideDevice SetCloaking: {0}", status);
            }
        }

        return true;
    }

    public static bool UnhidePath(string deviceInstancePath)
    {
        if (string.IsNullOrEmpty(deviceInstancePath))
            return false;

        try
        {
            var service = new HidHideControlService();
            if (service.BlockedInstanceIds.Contains(deviceInstancePath))
            {
                service.RemoveBlockedInstanceId(deviceInstancePath);
                LogManager.LogInformation("HideDevice RemoveBlockedInstanceId: {0}", deviceInstancePath);
            }
        }
        catch
        {
            LogManager.LogError("Failed to UnhidePath({0}), HidHideControlService is unreachable", deviceInstancePath);

            if (process is null)
                return false;

            process.StartInfo.Arguments = $"--dev-unhide \"{deviceInstancePath}\"";
            process.Start();
            bool success = process.WaitForExit(TimeSpan.FromSeconds(3));

            if (success)
            {
                process.StandardOutput.ReadToEnd(); // todo: parse result
                LogManager.LogInformation("HideDevice AddBlockedInstanceId: {0}", deviceInstancePath);
            }
        }

        return true;
    }

    public static bool HidePath(string deviceInstancePath)
    {
        if (string.IsNullOrEmpty(deviceInstancePath))
            return false;

        try
        {
            var service = new HidHideControlService();
            if (!service.BlockedInstanceIds.Contains(deviceInstancePath))
            {
                service.AddBlockedInstanceId(deviceInstancePath);
                LogManager.LogInformation("HideDevice AddBlockedInstanceId: {0}", deviceInstancePath);
            }
        }
        catch
        {
            LogManager.LogError("Failed to HidePath({0}), HidHideControlService is unreachable", deviceInstancePath);

            if (process is null)
                return false;

            process.StartInfo.Arguments = $"--dev-hide \"{deviceInstancePath}\"";
            process.Start();
            bool success = process.WaitForExit(TimeSpan.FromSeconds(3));

            if (success)
            {
                process.StandardOutput.ReadToEnd(); // todo: parse result
                LogManager.LogInformation("HideDevice AddBlockedInstanceId: {0}", deviceInstancePath);
            }
        }

        return true;
    }

    public static List<HidHideDevice> GetHidHideDevices(string arg = "--dev-all")
    {
        try
        {
            if (process is null)
                return null;

            // using --dev-gaming sometimes doesn't report controllers or have empty BaseContainerDeviceInstancePath
            process.StartInfo.Arguments = $"--dev-all";
            process.Start();
            bool success = process.WaitForExit(TimeSpan.FromSeconds(3));

            if (success)
            {
                string jsonString = process.StandardOutput.ReadToEnd().Trim();
                if (string.IsNullOrEmpty(jsonString))
                    return [];

                return JsonConvert.DeserializeObject<List<HidHideDevice>>(jsonString);
            }
        }
        catch { }

        return [];
    }

    public static HidHideDevice GetHidHideDevice(string deviceInstancePath)
    {
        List<HidHideDevice> hidHideDevices = GetHidHideDevices();

        if (hidHideDevices.Count != 0)
            return hidHideDevices.FirstOrDefault(device => device.Devices.Where(a => a.BaseContainerDeviceInstancePath == deviceInstancePath || a.DeviceInstancePath == deviceInstancePath).Any());

        return null;
    }
}

public partial class HidHideDevice
{
    [JsonProperty("friendlyName")]
    public string FriendlyName { get; set; }

    [JsonProperty("devices")]
    public HidHideSubDevice[] Devices { get; set; }
}

public partial class HidHideSubDevice
{
    [JsonProperty("present")]
    public bool Present { get; set; }

    [JsonProperty("gamingDevice")]
    public bool GamingDevice { get; set; }

    [JsonProperty("symbolicLink")]
    public string SymbolicLink { get; set; }

    [JsonProperty("vendor")]
    public string Vendor { get; set; }

    [JsonProperty("product")]
    public string Product { get; set; }

    [JsonProperty("serialNumber")]
    public string SerialNumber { get; set; }

    [JsonProperty("usage")]
    public string Usage { get; set; }

    [JsonProperty("description")]
    public string Description { get; set; }

    [JsonProperty("deviceInstancePath")]
    public string DeviceInstancePath { get; set; }

    [JsonProperty("baseContainerDeviceInstancePath")]
    public string BaseContainerDeviceInstancePath { get; set; }

    [JsonProperty("baseContainerClassGuid")]
    public string BaseContainerClassGuid { get; set; }

    [JsonProperty("baseContainerDeviceCount")]
    public long BaseContainerDeviceCount { get; set; }
}