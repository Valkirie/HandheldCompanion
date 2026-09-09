using HandheldCompanion.Devices.AYANEO;
using HandheldCompanion.Inputs;
using HandheldCompanion.Shared;
using HidLibrary;
using System.Windows.Media;
using WindowsInput.Events;

namespace HandheldCompanion.Devices;

public class AYANEONEXT2 : AYANEODeviceCEc
{
    private readonly AYANEOSuperJoyRgb superJoy = new();

    public AYANEONEXT2()
    {
        ProductIllustration = "device_aya_next2";
        ProductModel = "AYANEO NEXT 2";

        vendorId = AYANEOSuperJoyRgb.VendorId;
        productIds = [AYANEOSuperJoyRgb.ProductId];

        // AMD Ryzen AI Max 385 / Ryzen AI Max+ 395 (Strix Halo)
        // https://www.amd.com/en/products/processors/laptop/ryzen/ai-300-series/amd-ryzen-ai-max-plus-395.html
        // AYASpace DevicePowerTable: IsAyaNextII presets 15/30/45, range 5-85
        nTDP = new double[] { 15, 30, 45 };
        cTDP = new double[] { 5, 85 };
        GfxClock = new double[] { 100, 2900 };
        CpuClock = 5100;

        string processor = MotherboardInfo.ProcessorName;
        if (processor.Contains("385"))
        {
            GfxClock = new double[] { 100, 2800 };
            CpuClock = 5000;
        }

        foreach (var profile in DevicePowerProfiles)
        {
            if (profile.Guid == BetterBatteryGuid)
                profile.TDPOverrideValues = new[] { 15.0d, 15.0d, 15.0d };
            else if (profile.Guid == BetterPerformanceGuid)
                profile.TDPOverrideValues = new[] { 30.0d, 30.0d, 30.0d };
            else if (profile.Guid == BestPerformanceGuid)
                profile.TDPOverrideValues = new[] { 45.0d, 45.0d, 45.0d };
        }

        // IMU matrices are loaded from AYANEONEXT2.json.

        // https://github.com/ShadowBlip/InputPlumber/issues/654
        // Users will need to cycle the controller profiles by repeatedly pressing the LC + RC buttons
        // until the stick LED rings glow green for expected InputPlumber mappings to work.
        OEMChords.Clear();
        OEMChords.Add(new KeyboardChord("AYANEO Button", [KeyCode.F23], [KeyCode.F23], false, ButtonFlags.OEM1));
        OEMChords.Add(new KeyboardChord("Custom Key Small", [KeyCode.LWin, KeyCode.D], [KeyCode.LWin, KeyCode.D], false, ButtonFlags.OEM2));
        OEMChords.Add(new KeyboardChord("LC", [KeyCode.F21], [KeyCode.F21], false, ButtonFlags.OEM3));
        OEMChords.Add(new KeyboardChord("RC", [KeyCode.F22], [KeyCode.F22], false, ButtonFlags.OEM4));
        OEMChords.Add(new KeyboardChord("T", [KeyCode.F16], [KeyCode.F16], false, ButtonFlags.OEM5));
        OEMChords.Add(new KeyboardChord("LC1", [KeyCode.F19], [KeyCode.F19], false, ButtonFlags.OEM6));
        OEMChords.Add(new KeyboardChord("LC2", [KeyCode.F17], [KeyCode.F17], false, ButtonFlags.OEM7));
        OEMChords.Add(new KeyboardChord("RC1", [KeyCode.F20], [KeyCode.F20], false, ButtonFlags.OEM8));
        OEMChords.Add(new KeyboardChord("RC2", [KeyCode.F18], [KeyCode.F18], false, ButtonFlags.OEM9));
    }

    public override bool Open()
    {
        if (!base.Open())
            return false;

        lock (updateLock)
        {
            if (TryBindSuperJoy() && ledStatus is not null)
                ApplySuperJoyLeds();
        }

        return true;
    }

    public override void Close()
    {
        lock (updateLock)
            superJoy.Close();

        base.Close();
    }

    protected override void Device_Inserted(bool reScan = false)
    {
        if (superJoy.IsOpen)
            return;

        if (TryBindSuperJoy() && ledStatus is not null)
            ApplySuperJoyLeds();
    }

    protected override void Device_Removed()
    {
        if (!superJoy.IsOpen)
            superJoy.Close();
    }

    private bool TryBindSuperJoy()
    {
        if (superJoy.IsOpen)
            return true;

        bool interfaceFound = false;
        foreach (HidDevice device in GetHidDevices(vendorId, productIds, 0))
        {
            if (!AYANEOSuperJoyRgb.MatchesInterface(device))
                continue;

            interfaceFound = true;
            if (superJoy.Open(device))
                return true;
        }

        if (!interfaceFound)
            LogManager.LogWarning("SuperJoy stick RGB interface is unavailable");
        return false;
    }

    private void ApplySuperJoyLeds(bool? enabled = null)
    {
        Color left = ledColorSticksLeft ?? Colors.Black;
        Color right = ledColorStickRight ?? Colors.Black;
        int brightness = ledBrightness ?? 100;
        bool isEnabled = enabled ?? ledStatus ?? false;

        if (!superJoy.IsOpen && !TryBindSuperJoy())
            return;

        lock (HidWriteLock)
            superJoy.SetSticks(left, right, brightness, isEnabled);
        if (superJoy.IsOpen || !TryBindSuperJoy())
            return;

        lock (HidWriteLock)
            superJoy.SetSticks(left, right, brightness, isEnabled);
    }

    protected override void CEcRgb_GlobalOn(LEDGroup group, byte speed = 0x00)
    {
        if (group == LEDGroup.StickBoth)
        {
            ApplySuperJoyLeds(true);
            return;
        }

        base.CEcRgb_GlobalOn(group, speed);
    }

    protected override void CEcRgb_GlobalOff(LEDGroup group)
    {
        if (group == LEDGroup.StickBoth)
        {
            ApplySuperJoyLeds(false);
            return;
        }

        base.CEcRgb_GlobalOff(group);
    }

    protected override void CEcRgb_SetColorAll(LEDGroup group, Color color)
    {
        if (group is LEDGroup.StickLeft or LEDGroup.StickRight)
        {
            ApplySuperJoyLeds();
            return;
        }

        base.CEcRgb_SetColorAll(group, color);
    }

    public override string GetFontFamily(ButtonFlags button)
    {
        switch (button)
        {
            case ButtonFlags.OEM5:  // T
                return "Segoe Fluent Icons";
        }

        return base.GetFontFamily(button);
    }

    public override string GetGlyph(ButtonFlags button)
    {
        return button switch
        {
            ButtonFlags.OEM1 => "\uE003", // Ayaneo
            ButtonFlags.OEM2 => "\u220B", // Ayaneo Wave
            ButtonFlags.OEM3 => "\u2209", // Ayaneo LC
            ButtonFlags.OEM4 => "\u220A", // Ayaneo RC
            ButtonFlags.OEM5 => "\uF0E2", // GridView
            ButtonFlags.OEM6 => "\u2276", // SteamDeck L4
            ButtonFlags.OEM7 => "\u2278", // SteamDeck L5
            ButtonFlags.OEM8 => "\u2277", // SteamDeck R4
            ButtonFlags.OEM9 => "\u2279", // SteamDeck R5
            _ => base.GetGlyph(button)
        };
    }
}
