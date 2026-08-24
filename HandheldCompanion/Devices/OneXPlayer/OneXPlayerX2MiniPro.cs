using HandheldCompanion.Inputs;
using HandheldCompanion.Shared;
using System.Threading;
using System.Threading.Tasks;
using WindowsInput.Events;
using static HandheldCompanion.Utils.DeviceUtils;

namespace HandheldCompanion.Devices;

public class OneXPlayerX2MiniPro : OneXPlayerX2
{
    public OneXPlayerX2MiniPro()
    {
        // todo: ProductIllustration
        ProductIllustration = "device_onexplayer_x2minipro";
        ProductModel = "ONEXPLAYER X2Mini PRO";

        nTDP = new double[] { 15, 35, 54 };
        cTDP = new double[] { 15, 54 };

        EnableSerialPort = false;

        DynamicLightingCapabilities |= LEDLevel.Breathing;

        OEMChords.Add(new KeyboardChord("M1", [KeyCode.F15], [KeyCode.F15], false, ButtonFlags.L4));
        OEMChords.Add(new KeyboardChord("M2", [KeyCode.F16], [KeyCode.F16], false, ButtonFlags.R4));
    }

    protected override async Task ConfigureController()
    {
        // Equivalent to hid_v1.INITIALIZE_X2[0].
        WriteVendorHidCommand(0xB4, BuildRemapPage1(0x01));
        await Task.Delay(50);

        // Equivalent to hid_v1.INITIALIZE_X2[1], including M1/M2 -> F15/F16.
        WriteVendorHidCommand(0xB4, BuildRemapPage2(0x01, 0x68, 0x69));
        await Task.Delay(50);

        // Equivalent to hid_v1.INITIALIZE_X2[2], the required third partial page.
        WriteVendorHidCommand(0xB4, BuildRemapPage3());
        await Task.Delay(50);

        // Equivalent to hid_v1.gen_intercept(False), releasing vendor interception.
        WriteVendorHidCommand(0xB2, BuildIntercept(false));
    }

    protected override byte[] BuildRemapPage1(byte preset) =>
    [
        0x02, 0x38, 0x02, 0x01, preset,
        0x01, 0x01, 0x01, 0x00, 0x00, 0x00,
        0x02, 0x01, 0x02, 0x00, 0x00, 0x00,
        0x03, 0x01, 0x03, 0x00, 0x00, 0x00,
        0x04, 0x01, 0x04, 0x00, 0x00, 0x00,
        0x05, 0x01, 0x05, 0x00, 0x00, 0x00,
        0x06, 0x01, 0x06, 0x00, 0x00, 0x00,
        0x07, 0x01, 0x07, 0x00, 0x00, 0x00,
        0x08, 0x01, 0x08, 0x00, 0x00, 0x00,
        0x09, 0x01, 0x09, 0x00, 0x00, 0x00,
    ];

    protected override byte[] BuildRemapPage2(byte preset, byte m1KeyCode, byte m2KeyCode) =>
    [
        0x02, 0x38, 0x02, 0x02, preset,
        0x0A, 0x01, 0x0A, 0x00, 0x00, 0x00,
        0x0B, 0x01, 0x0B, 0x00, 0x00, 0x00,
        0x0C, 0x01, 0x0C, 0x00, 0x00, 0x00,
        0x0D, 0x01, 0x0D, 0x00, 0x00, 0x00,
        0x0E, 0x01, 0x0E, 0x00, 0x00, 0x00,
        0x0F, 0x01, 0x0F, 0x00, 0x00, 0x00,
        0x10, 0x01, 0x10, 0x00, 0x00, 0x00,
        0x22, 0x02, 0x01, m1KeyCode, 0x00, 0x00,
        0x23, 0x02, 0x01, m2KeyCode, 0x00, 0x00,
    ];

    protected byte[] BuildRemapPage3(byte preset = 0x01) =>
    [
        0x02, 0x38, 0x02, 0x03, preset,
        0x24, 0x02, 0x02, 0x05, 0x00, 0x00,
        0x25, 0x01, 0x21, 0x00, 0x00, 0x00,
    ];

    protected override void SetTurboButtonTakeover(bool enabled)
    {
        // do nothing; the X2 Mini Pro does not support ACPI/WMI interception of the Turbo button
        // it's instead handled by OneXAOKZOE Open()
    }

    public override bool SetLedBrightness(int brightness)
    {
        // HHD's x2 rgb_sides = (0x01, 0x02, 0x07) and secondary_sides = (0x05, 0x06).
        hidDevices.TryGetValue(VendorHidId, out HidLibrary.HidDevice? device);
        if (device is null)
            return false;

        bool result = true;
        foreach (byte side in new byte[] { 0x01, 0x02, 0x07, 0x05, 0x06 })
        {
            result &= SendV1Brightness(device, brightness, side);
            Thread.Sleep(100);
        }
        return result;
    }

    public override bool SetLedColor(System.Windows.Media.Color mainColor, System.Windows.Media.Color secondaryColor, LEDLevel level, int speed = 100)
    {
        if (level is not (LEDLevel.SolidColor or LEDLevel.Breathing))
            return false;

        hidDevices.TryGetValue(VendorHidId, out HidLibrary.HidDevice? device);
        if (device is null)
            return false;

        bool breathing = level == LEDLevel.Breathing;
        bool result = true;
        // HHD sends the primary color to the two joystick zones and center zone.
        foreach (byte side in new byte[] { 0x01, 0x02, 0x07 })
        {
            result &= SendV1SolidColor(device, mainColor, side, breathing);
            Thread.Sleep(100);
        }
        // There is intentionally no side 0 aggregate zone on the X2 Mini Pro.
        foreach (byte side in new byte[] { 0x05, 0x06 })
        {
            result &= SendV1SolidColor(device, secondaryColor, side, breathing);
            Thread.Sleep(100);
        }
        return result;
    }
}
