using HandheldCompanion.Models;
using HandheldCompanion.Shared;
using System;
using System.IO;
using System.Text.Json;

namespace HandheldCompanion.Helpers
{
    public static class DeviceConfigurationHelper
    {
        private static readonly string ConfigsDirectory =
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "Devices");

        public static DeviceConfiguration LoadConfiguration(string deviceClassName)
        {
            return LoadConfigurationRecursive(deviceClassName, deviceClassName);
        }

        private static DeviceConfiguration LoadConfigurationRecursive(string classNameToTry, string originalDeviceClassName)
        {
            var filePath = Path.Combine(ConfigsDirectory, $"{classNameToTry}.json");

            if (File.Exists(filePath))
            {
                try
                {
                    var json = File.ReadAllText(filePath);
                    var config = JsonSerializer.Deserialize<DeviceConfiguration>(json);
                    if (config != null)
                    {
                        config.DeviceClass = originalDeviceClassName;
                        LogManager.LogDebug($"Device configuration loaded: {originalDeviceClassName}");
                        return config;
                    }
                }
                catch (Exception ex)
                {
                    LogManager.LogError($"Failed to load device config {classNameToTry}: {ex.Message}");
                }
            }

            // Try parent class via reflection
            try
            {
                var type = Type.GetType($"HandheldCompanion.Devices.{classNameToTry}");
                var parentType = type?.BaseType;

                if (parentType != null && parentType != typeof(object) && !parentType.IsInterface)
                {
                    LogManager.LogDebug($"Device configuration not found: {classNameToTry}, trying parent: {parentType.Name}");
                    return LoadConfigurationRecursive(parentType.Name, originalDeviceClassName);
                }
            }
            catch
            { }

            LogManager.LogDebug($"Device configuration not found: {originalDeviceClassName}");
            return new DeviceConfiguration { DeviceClass = originalDeviceClassName };
        }

        public static void SaveConfiguration(DeviceConfiguration config)
        {
            var filePath = Path.Combine(ConfigsDirectory, $"{config.DeviceClass}.json");

            try
            {
                Directory.CreateDirectory(ConfigsDirectory);
                var options = new JsonSerializerOptions { WriteIndented = true };
                var json = JsonSerializer.Serialize(config, options);
                File.WriteAllText(filePath, json);

                LogManager.LogDebug($"Device configuration saved: {config.DeviceClass}");
            }
            catch (Exception ex)
            {
                LogManager.LogError($"Failed to save device config {config.DeviceClass}: {ex.Message}");
            }
        }
    }
}
