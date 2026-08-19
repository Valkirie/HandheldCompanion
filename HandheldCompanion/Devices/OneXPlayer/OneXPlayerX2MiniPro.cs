using HandheldCompanion.Inputs;
using HandheldCompanion.Shared;
using System.Threading;
using WindowsInput.Events;
using static HandheldCompanion.Utils.DeviceUtils;

namespace HandheldCompanion.Devices;

public class OneXPlayerX2MiniPro : OneXPlayerX2
{
    public OneXPlayerX2MiniPro()
    {
        // todo: ProductIllustration
        // ProductIllustration = "device_onexplayer_apex";
        ProductModel = "ONEXPLAYER X2Mini PRO";

        nTDP = new double[] { 15, 35, 54 };
        cTDP = new double[] { 15, 54 };
        DynamicLightingCapabilities |= LEDLevel.Breathing;

        OEMChords.Add(new KeyboardChord("M1", [KeyCode.F15], [KeyCode.F15], false, ButtonFlags.L4));
        OEMChords.Add(new KeyboardChord("M2", [KeyCode.F16], [KeyCode.F16], false, ButtonFlags.R4));
    }

    protected override void InitializeVendorHidCommands()
    {
        // This is HHD's hid_v2_x2 configuration, implemented by OxpHidraw
        // with x2=True. The Mini Pro requires all three pages; the first two
        // alone are ignored by the firmware.
        HidLibrary.HidDevice? device = GetVendorHidDeviceForLighting();
        if (device is null)
        {
            LogManager.LogWarning("X2 Mini Pro vendor HID device is not bound; initialization skipped");
            return;
        }

        LogManager.LogInformation(
            "X2 Mini Pro vendor HID bound: PID 0x{0:X4}, usage page 0x{1:X4}, usage 0x{2:X4}, output report length {3}",
            device.Attributes.ProductId,
            device.Capabilities.UsagePage,
            device.Capabilities.Usage,
            device.Capabilities.OutputReportByteLength);

        System.Threading.Thread.Sleep(4000);
        // Equivalent to hid_v1.INITIALIZE_X2[0].
        WriteMiniProHidCommand(0xB4,
        [
            0x02, 0x38, 0x02, 0x01, 0x01,
            0x01, 0x01, 0x01, 0x00, 0x00, 0x00,
            0x02, 0x01, 0x02, 0x00, 0x00, 0x00,
            0x03, 0x01, 0x03, 0x00, 0x00, 0x00,
            0x04, 0x01, 0x04, 0x00, 0x00, 0x00,
            0x05, 0x01, 0x05, 0x00, 0x00, 0x00,
            0x06, 0x01, 0x06, 0x00, 0x00, 0x00,
            0x07, 0x01, 0x07, 0x00, 0x00, 0x00,
            0x08, 0x01, 0x08, 0x00, 0x00, 0x00,
            0x09, 0x01, 0x09, 0x00, 0x00, 0x00,
        ]);
        System.Threading.Thread.Sleep(50);
        // Equivalent to hid_v1.INITIALIZE_X2[1], including M1/M2 -> F15/F16.
        WriteMiniProHidCommand(0xB4,
        [
            0x02, 0x38, 0x02, 0x02, 0x01,
            0x0A, 0x01, 0x0A, 0x00, 0x00, 0x00,
            0x0B, 0x01, 0x0B, 0x00, 0x00, 0x00,
            0x0C, 0x01, 0x0C, 0x00, 0x00, 0x00,
            0x0D, 0x01, 0x0D, 0x00, 0x00, 0x00,
            0x0E, 0x01, 0x0E, 0x00, 0x00, 0x00,
            0x0F, 0x01, 0x0F, 0x00, 0x00, 0x00,
            0x10, 0x01, 0x10, 0x00, 0x00, 0x00,
            0x22, 0x02, 0x01, 0x68, 0x00, 0x00,
            0x23, 0x02, 0x01, 0x69, 0x00, 0x00,
        ]);
        System.Threading.Thread.Sleep(50);
        // Equivalent to hid_v1.INITIALIZE_X2[2], the required third partial page.
        WriteMiniProHidCommand(0xB4,
        [0x02, 0x38, 0x02, 0x03, 0x01, 0x24, 0x02, 0x02, 0x05, 0x00, 0x00, 0x25, 0x01, 0x21, 0x00, 0x00, 0x00]);
        System.Threading.Thread.Sleep(50);
        // Equivalent to hid_v1.gen_intercept(False), releasing vendor interception.
        WriteMiniProHidCommand(0xB2, [0x00, 0x01, 0x02]);
        LogManager.LogInformation("X2 Mini Pro vendor HID initialization sequence completed");
    }

    private bool WriteMiniProHidCommand(byte commandId, byte[] payload)
    {
        bool result = WriteVendorHidCommand(commandId, payload);
        if (!result)
            LogManager.LogWarning("X2 Mini Pro HID write failed: command 0x{0:X2}, payload length {1}", commandId, payload.Length);
        else
            LogManager.LogDebug("X2 Mini Pro HID write succeeded: command 0x{0:X2}, payload length {1}", commandId, payload.Length);
        return result;
    }

    protected override void HandleEvent(byte buttonId, bool pressed)
    {
        if (buttonId is 0x22 or 0x23)
            LogManager.LogInformation("X2 Mini Pro vendor paddle report: button 0x{0:X2}, pressed {1}", buttonId, pressed);

        base.HandleEvent(buttonId, pressed);
    }

    public override bool SetLedBrightness(int brightness)
    {
        // HHD's x2 rgb_sides = (0x01, 0x02, 0x07) and secondary_sides = (0x05, 0x06).
        bool result = true;
        foreach (byte side in new byte[] { 0x01, 0x02, 0x07, 0x05, 0x06 })
            result &= SendV1Brightness(GetVendorHidDeviceForLighting(), brightness, side);
        return result;
    }

    public override bool SetLedColor(System.Windows.Media.Color mainColor, System.Windows.Media.Color secondaryColor, LEDLevel level, int speed = 100)
    {
        if (level is not (LEDLevel.SolidColor or LEDLevel.Breathing))
            return false;

        bool breathing = level == LEDLevel.Breathing;
        bool result = true;
        // HHD sends the primary color to the two joystick zones and center zone.
        foreach (byte side in new byte[] { 0x01, 0x02, 0x07 })
            result &= SendV1SolidColor(GetVendorHidDeviceForLighting(), mainColor, side, breathing);
        // There is intentionally no side 0 aggregate zone on the X2 Mini Pro.
        foreach (byte side in new byte[] { 0x05, 0x06 })
            result &= SendV1SolidColor(GetVendorHidDeviceForLighting(), secondaryColor, side, breathing);
        return result;
    }
}
