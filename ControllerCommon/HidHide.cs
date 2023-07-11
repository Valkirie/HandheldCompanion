using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using ControllerCommon.Managers;
using ControllerCommon.Utils;
using Nefarius.Drivers.HidHide;
using Newtonsoft.Json;

namespace ControllerCommon;

public static class HidHide
{
    private static readonly Process process;

    static HidHide()
    {
        var service = new HidHideControlService();

        // verifying HidHide is installed
        if (!service.IsInstalled)
        {
            LogManager.LogCritical("HidHide is missing. Please get it from: {0}",
                "https://github.com/ViGEm/HidHide/releases");
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

        return new List<string>();
    }

    public static List<string> GetRegisteredDevices()
    {
        try
        {
            var service = new HidHideControlService();
            return service.BlockedInstanceIds.ToList();
        }
        catch
        {
        }

        return new List<string>();
    }

    public static bool IsRegistered(string InstanceId)
    {
        try
        {
            var registered = GetRegisteredDevices();
            return registered.Contains(InstanceId);
        }
        catch
        {
        }

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
        catch
        {
        }
    }

    public static void UnhidePath(string deviceInstancePath)
    {
        if (string.IsNullOrEmpty(deviceInstancePath))
            return;

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
        }
    }

    public static void HidePath(string deviceInstancePath)
    {
        if (string.IsNullOrEmpty(deviceInstancePath))
            return;

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
        }
    }

    public static List<HidHideDevice> GetHidHideDevices()
    {
        try
        {
            if (process is null)
                return null;

            process.StartInfo.Arguments = $"--dev-gaming";
            process.Start();
            process.WaitForExit(TimeSpan.FromSeconds(3));
            string jsonString = process.StandardOutput.ReadToEnd().Trim();

            if (string.IsNullOrEmpty(jsonString))
                return null;

            return JsonConvert.DeserializeObject<List<HidHideDevice>>(jsonString);
        }
        catch { }

        return null;
    }

    public static HidHideDevice GetHidHideDevice(string deviceInstancePath)
    {
        List<HidHideDevice> hidHideDevices = GetHidHideDevices();

        if (hidHideDevices.Count != 0)
            return hidHideDevices.FirstOrDefault(device => device.Devices.Where(a => a.BaseContainerDeviceInstancePath == deviceInstancePath).Any());

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