using HandheldCompanion.Shared;
using HandheldCompanion.Utils;
using Nefarius.Drivers.HidHide;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;

namespace HandheldCompanion;

public static class HidHide
{
    private static readonly Process? process;
    private static readonly object hidLock = new();
    private const int HidLockTimeoutMs = 3000;

    static HidHide()
    {
        HidHideControlService service = new HidHideControlService();

        // verifying HidHide is installed
        if (!service.IsInstalled)
        {
            LogManager.LogCritical("HidHide is missing. Please get it from: {0}", "https://github.com/ViGEm/HidHide/releases");
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
            if (!Monitor.TryEnter(hidLock, HidLockTimeoutMs))
                throw new TimeoutException();

            try
            {
                HidHideControlService service = new HidHideControlService();
                return service.ApplicationPaths.ToList();
            }
            finally
            {
                Monitor.Exit(hidLock);
            }
        }
        catch { }

        return [];
    }

    public static List<string> GetRegisteredDevices()
    {
        try
        {
            if (!Monitor.TryEnter(hidLock, HidLockTimeoutMs))
                throw new TimeoutException();

            try
            {
                HidHideControlService service = new HidHideControlService();
                return service.BlockedInstanceIds.Select(x => x.ToUpper()).ToList();
            }
            finally
            {
                Monitor.Exit(hidLock);
            }
        }
        catch { }

        return [];
    }

    public static bool IsRegistered(string InstanceId)
    {
        try
        {
            List<string> registered = GetRegisteredDevices();
            return registered.Contains(InstanceId.ToUpper());
        }
        catch { }

        return false;
    }

    public static bool UnregisterApplication(string fileName)
    {
        try
        {
            if (!Monitor.TryEnter(hidLock, HidLockTimeoutMs))
                throw new TimeoutException();

            try
            {
                HidHideControlService service = new HidHideControlService();
                if (service.ApplicationPaths.Contains(fileName))
                {
                    service.RemoveApplicationPath(fileName);
                    LogManager.LogInformation("HideDevice RemoveApplicationPath: {0}", fileName);
                }
            }
            finally
            {
                Monitor.Exit(hidLock);
            }
        }
        catch
        {
            LogManager.LogError("Failed to UnregisterApplication({0}), HidHideControlService is unreachable", fileName);

            if (process is null)
                return false;

            process.StartInfo.Arguments = $"--app-unreg \"{fileName}\"";
            bool started = process.Start();
            bool success = process.WaitForExit(TimeSpan.FromSeconds(3));

            if (started && success)
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
            if (!Monitor.TryEnter(hidLock, HidLockTimeoutMs))
                throw new TimeoutException();

            try
            {
                HidHideControlService service = new HidHideControlService();
                if (!service.ApplicationPaths.Contains(fileName))
                {
                    service.AddApplicationPath(fileName);
                    LogManager.LogInformation("HideDevice AddApplicationPath: {0}", fileName);
                }
            }
            finally
            {
                Monitor.Exit(hidLock);
            }
        }
        catch
        {
            LogManager.LogError("Failed to RegisterApplication({0}), HidHideControlService is unreachable", fileName);

            if (process is null)
                return false;

            process.StartInfo.Arguments = $"--app-reg \"{fileName}\"";
            bool started = process.Start();
            bool success = process.WaitForExit(TimeSpan.FromSeconds(3));

            if (started && success)
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
            if (!Monitor.TryEnter(hidLock, HidLockTimeoutMs))
                throw new TimeoutException();

            try
            {
                HidHideControlService service = new HidHideControlService { IsActive = status };
                LogManager.LogInformation("HideDevice IsActive: {0}", status);
            }
            finally
            {
                Monitor.Exit(hidLock);
            }
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
            bool started = process.Start();
            bool success = process.WaitForExit(TimeSpan.FromSeconds(3));

            if (started && success)
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
            if (!Monitor.TryEnter(hidLock, HidLockTimeoutMs))
                throw new TimeoutException();

            try
            {
                HidHideControlService service = new HidHideControlService();
                if (service.BlockedInstanceIds.Contains(deviceInstancePath))
                {
                    service.RemoveBlockedInstanceId(deviceInstancePath);
                    LogManager.LogInformation("HideDevice RemoveBlockedInstanceId: {0}", deviceInstancePath);
                }
            }
            finally
            {
                Monitor.Exit(hidLock);
            }
        }
        catch
        {
            LogManager.LogError("Failed to UnhidePath({0}), HidHideControlService is unreachable", deviceInstancePath);

            if (process is null)
                return false;

            process.StartInfo.Arguments = $"--dev-unhide \"{deviceInstancePath}\"";
            bool started = process.Start();
            bool success = process.WaitForExit(TimeSpan.FromSeconds(3));

            if (started && success)
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
            if (!Monitor.TryEnter(hidLock, HidLockTimeoutMs))
                throw new TimeoutException();

            try
            {
                HidHideControlService service = new HidHideControlService();
                if (!service.BlockedInstanceIds.Contains(deviceInstancePath))
                {
                    service.AddBlockedInstanceId(deviceInstancePath);
                    LogManager.LogInformation("HideDevice AddBlockedInstanceId: {0}", deviceInstancePath);
                }
            }
            finally
            {
                Monitor.Exit(hidLock);
            }
        }
        catch
        {
            LogManager.LogError("Failed to HidePath({0}), HidHideControlService is unreachable", deviceInstancePath);

            if (process is null)
                return false;

            process.StartInfo.Arguments = $"--dev-hide \"{deviceInstancePath}\"";
            bool started = process.Start();
            bool success = process.WaitForExit(TimeSpan.FromSeconds(3));

            if (started && success)
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
                return [];

            // using --dev-gaming sometimes doesn't report controllers or have empty BaseContainerDeviceInstancePath
            process.StartInfo.Arguments = $"--dev-all";
            bool started = process.Start();
            bool success = process.WaitForExit(TimeSpan.FromSeconds(3));

            if (started && success)
            {
                string jsonString = process.StandardOutput.ReadToEnd().Trim();
                if (string.IsNullOrEmpty(jsonString))
                    return [];

                return JsonConvert.DeserializeObject<List<HidHideDevice>>(jsonString) ?? [];
            }
        }
        catch { }

        return [];
    }

    public static HidHideDevice? GetHidHideDevice(string deviceInstancePath)
    {
        List<HidHideDevice> hidHideDevices = GetHidHideDevices();

        if (hidHideDevices.Count != 0)
            return hidHideDevices.FirstOrDefault(device => device.Devices.Any(a => a.BaseContainerDeviceInstancePath == deviceInstancePath || a.DeviceInstancePath == deviceInstancePath));

        return null;
    }
}

public partial class HidHideDevice
{
    [JsonProperty("friendlyName")]
    public string FriendlyName { get; set; } = string.Empty;

    [JsonProperty("devices")]
    public HidHideSubDevice[] Devices { get; set; } = [];
}

public partial class HidHideSubDevice
{
    [JsonProperty("present")]
    public bool Present { get; set; }

    [JsonProperty("gamingDevice")]
    public bool GamingDevice { get; set; }

    [JsonProperty("symbolicLink")]
    public string SymbolicLink { get; set; } = string.Empty;

    [JsonProperty("vendor")]
    public string Vendor { get; set; } = string.Empty;

    [JsonProperty("product")]
    public string Product { get; set; } = string.Empty;

    [JsonProperty("serialNumber")]
    public string SerialNumber { get; set; } = string.Empty;

    [JsonProperty("usage")]
    public string Usage { get; set; } = string.Empty;

    [JsonProperty("description")]
    public string Description { get; set; } = string.Empty;

    [JsonProperty("deviceInstancePath")]
    public string DeviceInstancePath { get; set; } = string.Empty;

    [JsonProperty("baseContainerDeviceInstancePath")]
    public string BaseContainerDeviceInstancePath { get; set; } = string.Empty;

    [JsonProperty("baseContainerClassGuid")]
    public string BaseContainerClassGuid { get; set; } = string.Empty;

    [JsonProperty("baseContainerDeviceCount")]
    public long BaseContainerDeviceCount { get; set; }
}