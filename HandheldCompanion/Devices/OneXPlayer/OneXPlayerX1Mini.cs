using HidLibrary;
using System.Collections.Generic;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media;
using static HandheldCompanion.Utils.DeviceUtils;

namespace HandheldCompanion.Devices;

public class OneXPlayerX1Mini : OneXPlayerX1
{
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

        // vendor button/LED interface (0x1A86 / 0xFE00, usage FF00:0001) is inherited from OneXPlayerX1
    }

    protected override async Task ConfigureController()
    {
        WriteVendorHidCommand(VibrationCommandId, [0x01, 0x05, 0x05]);
    }

    protected override void HandleStatusReport(byte[] report)
    {
        if (report[3] == 0xFE)
        {
            Device_Removed();
            Device_Inserted(true);
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
