﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Management;

namespace HandheldCompanion.Utils;

public class DeviceUtils
{
    public enum SensorFamily
    {
        None = 0,
        Windows = 1,
        SerialUSBIMU = 2,
        Controller = 3
    }

    [Flags]
    public enum LEDLevel
    {
        SolidColor = 0,
        Breathing = 1,
        Rainbow = 2,
        Wave = 4,
        Wheel = 8,
        Gradient = 16,
        Ambilight = 32,
        LEDPreset = 64,
    }

    public enum LEDDirection
    {
        Up,
        Down,
        Left,
        Right
    }

    public static USBDeviceInfo GetUSBDevice(string DeviceId)
    {
        try
        {
            using (var searcher =
                   new ManagementObjectSearcher(
                       $"SELECT * From Win32_PnPEntity WHERE DeviceId = '{DeviceId.Replace("\\", "\\\\")}'"))
            {
                var devices = searcher.Get().Cast<ManagementBaseObject>().ToList();
                return new USBDeviceInfo(devices.FirstOrDefault());
            }
        }
        catch
        {
        }

        return null;
    }

    public static List<USBDeviceInfo> GetSerialDevices()
    {
        List<USBDeviceInfo> serials = new List<USBDeviceInfo>();
        try
        {
            using (ManagementObjectSearcher searcher = new ManagementObjectSearcher("SELECT * FROM Win32_PnPEntity WHERE Name LIKE '%COM%' AND PNPClass = 'Ports'"))
            {
                List<ManagementBaseObject> devices = searcher.Get().Cast<ManagementBaseObject>().ToList();
                foreach (ManagementBaseObject device in devices)
                    serials.Add(new USBDeviceInfo(device));
            }
        }
        catch { }

        return serials;
    }

    public static void RestartComputer()
    {
        Process.Start(new ProcessStartInfo("shutdown", "/r /f /t 0")
        {
            CreateNoWindow = true,
            UseShellExecute = false
        });
    }
}