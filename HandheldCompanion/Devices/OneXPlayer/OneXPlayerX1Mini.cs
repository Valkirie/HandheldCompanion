using HidLibrary;
using System.Collections.Generic;
using System.Numerics;
using System.Threading;
using System.Windows.Media;
using static HandheldCompanion.Utils.DeviceUtils;

namespace HandheldCompanion.Devices;

public class OneXPlayerX1Mini : OneXPlayerX1
{
    protected const int PID_LED = 0xFE00;

    public OneXPlayerX1Mini()
    {
        // https://www.amd.com/fr/products/processors/laptop/ryzen/8000-series/amd-ryzen-7-8840u.html
        nTDP = new double[] { 15, 15, 28 };
        cTDP = new double[] { 15, 30 };
        GfxClock = new double[] { 100, 2700 };
        CpuClock = 5100;

        GyroMatrix = new() { Axis = new Vector3(1.0f, 1.0f, 1.0f) };
        AcceleroMatrix = new() { Axis = new Vector3(1.0f, -1.0f, 1.0f) };

        EnableSerialPort = false;

        // device specific capacities
        Capabilities |= DeviceCapabilities.DynamicLighting;
        Capabilities |= DeviceCapabilities.DynamicLightingBrightness;

        // dynamic lighting capacities
        DynamicLightingCapabilities |= LEDLevel.SolidColor;
        DynamicLightingCapabilities |= LEDLevel.LEDPreset;
        DynamicLightingCapabilities |= LEDLevel.Rainbow;

        // LED HID device (HID v1, vendor device on 0x1A86 / 0xFE00 / FF00:0001)
        vendorId = 0x1A86;
        productIds = [PID_LED];
        hidFilters = new()
        {
            { PID_LED, new HidFilter(unchecked((short)0xFF00), unchecked(0x0001)) },
        };
    }

    protected override async void Device_Inserted(bool reScan = false)
    {
        if (reScan)
            await WaitUntilReady();

        base.Device_Inserted();
        Thread.Sleep(50);
        WriteVendorHidCommand(VibrationCommandId, [0x01, 0x05, 0x05]);
    }

    public override bool IsReady()
    {
        // Early return if device is already bound and connected
        if (hidDevices.TryGetValue(VendorHidId, out HidDevice? boundDevice) && boundDevice.IsConnected)
            return true;

        // Reuse the same pattern as OneXPlayerOneXFly to grab the LED HID device
        IEnumerable<HidDevice> devices = GetHidDevices(vendorId, productIds, 0);
        foreach (HidDevice device in devices)
        {
            if (!device.IsConnected)
                continue;

            if (!hidFilters.TryGetValue(device.Attributes.ProductId, out HidFilter hidFilter))
                continue;

            if (device.Capabilities.UsagePage != hidFilter.UsagePage ||
                device.Capabilities.Usage != hidFilter.Usage)
                continue;

            hidDevices[VendorHidId] = device;
            return true;
        }

        return false;
    }

    protected override void HandleStatusReport(byte[] report)
    {
        if (report[3] == 0xFE)
        {
            Device_Removed();
            Device_Inserted();
        }
    }

    public override bool SetLedColor(Color mainColor, Color secondaryColor, LEDLevel level, int speed = 100)
    {
        if (!DynamicLightingCapabilities.HasFlag(level))
            return false;

        return level switch
        {
            LEDLevel.SolidColor => SendV1SolidColor(GetVendorHidDevice(), mainColor, 0x00),
            LEDLevel.Rainbow => SendV1Rainbow(GetVendorHidDevice(), 0x00),
            _ => false
        };
    }

    public override bool SetLedBrightness(int brightness)
    {
        // X1 Mini uses the HID v1 vendor protocol (cid 0xB8, "brightness" command)
        return SendV1Brightness(GetVendorHidDevice(), brightness, 0x00);
    }

    private HidDevice? GetVendorHidDevice()
    {
        return hidDevices.GetValueOrDefault(VendorHidId);
    }

    public override bool IsBatteryProtectionSupported(int majorVersion, int minorVersion)
    {
        return majorVersion >= 1 && minorVersion >= 3;
    }
}
