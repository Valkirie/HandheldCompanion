﻿using HidLibrary;
using System;
using System.Linq;
using System.Windows.Media;
using static HandheldCompanion.Utils.DeviceUtils;

namespace HandheldCompanion.Devices;

public class AOKZOEA2 : AOKZOEA1Pro
{
    HidDevice hidDevice;

    public AOKZOEA2()
    {
        // device specific settings
        ProductModel = "AOKZOEA2";

        // device specific capacities
        Capabilities |= DeviceCapabilities.DynamicLighting;
        Capabilities |= DeviceCapabilities.DynamicLightingBrightness;
        DynamicLightingCapabilities |= LEDLevel.SolidColor;
        DynamicLightingCapabilities |= LEDLevel.Rainbow;

        // LED HID Device
        _vid = 0x1A2C;
        _pid = 0xB001;
    }
    public override bool IsReady()
    {
        // Prepare list for all HID devices
        HidDevice[] HidDeviceList = HidDevices.Enumerate(_vid, new int[] { _pid }).ToArray();

        // Check every HID device to find LED device
        foreach (HidDevice device in HidDeviceList)
        {
            // OneXFly device for LED control does not support a FeatureReport, hardcoded to match the Interface Number
            if (device.IsConnected && device.DevicePath.Contains("&mi_00"))
            {
                hidDevice = device;
                return true;
            }
        }

        return false;
    }

    public override bool SetLedBrightness(int brightness)
    {
        // OneXFly brightness range is: 0 - 4 range, 0 is off, convert from 0 - 100 % range
        brightness = (int)Math.Round(brightness / 20.0);

        // Check if device is availible
        if (hidDevice is null || !hidDevice.IsConnected)
            return false;

        // Define the HID message for setting brightness.
        byte[] msg = { 0x00, 0x07, 0xFF, 0xFD, 0x01, 0x05, (byte)brightness };

        // Write the HID message to set the LED brightness.
        hidDevice.Write(msg);

        return true;
    }

    public override bool SetLedColor(Color mainColor, Color secondaryColor, LEDLevel level, int speed = 100)
    {
        if (!DynamicLightingCapabilities.HasFlag(level))
            return false;

        // Data message consists of a prefix, LED option, RGB data, and closing byte (0x00)
        byte[] prefix = { 0x00, 0x07, 0xFF };
        byte[] LEDOption = { 0x00 };
        byte[] rgbData = { 0x00 };

        // Perform functions and command build-up based on LED level
        switch (level)
        {
            case LEDLevel.SolidColor:
                // Find nearest possible color due to RGB limitations of the device
                Color ledColor = FindClosestColor(mainColor);

                LEDOption = new byte[] { 0xFE };

                // RGB data repeats 20 times, fill accordingly
                rgbData = Enumerable.Repeat(new[] { ledColor.R, ledColor.G, ledColor.B }, 20)
                                    .SelectMany(colorBytes => colorBytes)
                                    .ToArray();
                break;

            case LEDLevel.Rainbow:
                // OneXConsole "Flowing Light" effect as a rainbow effect
                LEDOption = new byte[] { 0x03 };

                // RGB data empty, repeats 60 times, fill accordingly
                rgbData = Enumerable.Repeat((byte)0x00, 60).ToArray();
                break;

            default:
                return false;
        }

        // Check if device is availible
        if (hidDevice is null || !hidDevice.IsConnected)
            return false;

        // Combine prefix, LED Option, RGB data, and closing byte (0x00)
        byte[] msg = prefix.Concat(LEDOption).Concat(rgbData).Concat(new byte[] { 0x00 }).ToArray();

        // Write the HID message to set the RGB color effect
        hidDevice.Write(msg);

        return true;
    }

    static Color FindClosestColor(Color inputColor)
    {
        // Predefined colors that work on the device
        Color[] predefinedColors = new Color[]
        {
            Color.FromRgb(255, 0, 0),         // Red (255,0,0) 
            Color.FromRgb(255, 82, 0),        // Orange (255, 165, 0)
            Color.FromRgb(255, 255, 0),       // Yellow (255, 255, 0)
            Color.FromRgb(130, 255, 0),       // Lime Green (50, 205, 50)
            Color.FromRgb(0, 255, 0),         // Green (0, 128, 0)
            Color.FromRgb(0, 255, 110),       // Turquoise (Cyan) (0, 255, 255)
            Color.FromRgb(0, 255, 255),       // Teal (0, 128, 128)
            Color.FromRgb(130, 255, 255),     // Blue (? ? ?)
            Color.FromRgb(0, 0, 255),         // Dark Blue (? ? ?)
            Color.FromRgb(122, 0, 255),       // Purple (Violet) (128, 0, 128)
            Color.FromRgb(255, 0, 255),       // Pink (255, 182, 193)
            Color.FromRgb(255, 0, 129),       // Magenta (255, 0, 255)
        };

        // Initialize with the first color
        Color closestColor = predefinedColors[0];
        double minDistance = CalculateDistance(inputColor, closestColor);

        // Iterate through predefined colors to find the closest one
        foreach (var predefinedColor in predefinedColors)
        {
            double distance = CalculateDistance(inputColor, predefinedColor);

            // Update closest color if a closer one is found
            if (distance < minDistance)
            {
                minDistance = distance;
                closestColor = predefinedColor;
            }
        }

        // Return the closest predefined color
        return closestColor;
    }

    static double CalculateDistance(Color color1, Color color2)
    {
        // Helper method to calculate the Euclidean distance between two colors

        int deltaR = color2.R - color1.R;
        int deltaG = color2.G - color1.G;
        int deltaB = color2.B - color1.B;

        // Euclidean distance formula
        return Math.Sqrt(deltaR * deltaR + deltaG * deltaG + deltaB * deltaB);
    }
}