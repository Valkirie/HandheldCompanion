using System;
using System.Linq;
using System.Management;
using System.Numerics;
using HandheldCompanion.Commands.Functions.Windows;
using HandheldCompanion.Inputs;
using HandheldCompanion.Managers;
using HandheldCompanion.Misc;
using HandheldCompanion.Shared;
using WindowsInput.Events;
using static HandheldCompanion.IGCL.IGCLBackend;
namespace HandheldCompanion.Devices;

public class OneXPlayerX2 : OneXPlayerX1
{
    // X2 uses the banked WMI EC address. OneXConsole initializes its application
    // function/turbo register as decimal 1259 (0x04EB), not legacy port address 0xEB.
    private const ushort TurboTakeoverRegister = 0x04EB;
    private const byte TurboTakeoverMask = 0x40;

    public OneXPlayerX2()
    {
        // device specific settings
        // NOTE: reuses the X1 illustration until a dedicated device_onexplayer_x2 asset is added.
        ProductIllustration = "device_onexplayer_x1";
        ProductModel = "ONEXPLAYERX2";

        // ---------------------------------------------------------------------
        // Power / thermal envelope
        // ---------------------------------------------------------------------
        // SoC: Intel Arc G3 Extreme ("Panther Lake", Core Ultra X7 358H class).
        //   - Family 6, Model 204 (0xCC), Stepping 2. 14 cores (2 Cougar Cove P
        //     + 8 Darkmont E + 4 Darkmont LP-E). Base 1.9 GHz, max turbo 4.7 GHz.
        //   - iGPU: Intel Arc B390 (Xe3 / Battlemage), 12 Xe cores, up to ~2.3 GHz.
        //   - Intel-rated Processor Base Power 15-25 W, Max Turbo Power up to 80 W,
        //     configurable-TDP envelope 8-35 W. This is a 35 W-class handheld.
        // Read live on this unit: MaxClockSpeed 1900 (base), GPU "Intel(R) Arc(TM)
        //   B390 GPU", EC version 0.11.
        // nTDP = { sustained (PL1), default, turbo (PL2) }.
        nTDP = new double[] { 25, 25, 35 };
        // cTDP slider range exposed by the UI. Floor 3 W for a deep battery-saver setting; ceiling
        //   35 W matches Intel's rated configurable-TDP cap and OneXPlayer's own OneXConsole, which
        //   tops out at 35 W (the SoC's PL2/Max Turbo still spikes higher on its own).
        cTDP = new double[] { 3, 35 };
        // iGPU (Arc B390) clock range in MHz.
        GfxClock = new double[] { 100, 2300 };
        // Max CPU turbo clock in MHz.
        CpuClock = 4700;

        // Intel power profiles (mirrors OneXPlayerX1Intel, tuned for Arc G3 Extreme).
        // Reuses the existing generic Intel profile resource strings
        // ("Efficiency" / "Performance" / "Super Performance") - no resx changes.

        // Power Saving
        DevicePowerProfiles.Add(new(Properties.Resources.PowerProfileOneXPlayerX1IntelBetterBattery, Properties.Resources.PowerProfileOneXPlayerX1IntelBetterBatteryDesc)
        {
            Default = true,
            DeviceDefault = true,
            OSPowerMode = OSPowerMode.BetterBattery,
            CPUBoostLevel = CPUBoostLevel.Disabled,
            Guid = BetterBatteryGuid,
            TDPOverrideEnabled = true,
            TDPOverrideValues = new[] { 15.0d, 15.0d, 15.0d },
            // Intel Arc "Endurance Gaming" — dynamic GPU power/FPS cap (like the MSI Claw). MAX ~30fps.
            IntelEnduranceGamingEnabled = true,
            IntelEnduranceGamingPreset = (int)ctl_3d_endurance_gaming_mode_t.MAX
        });

        // Performance
        DevicePowerProfiles.Add(new(Properties.Resources.PowerProfileOneXPlayerX1IntelBetterPerformance, Properties.Resources.PowerProfileOneXPlayerX1IntelBetterPerformanceDesc)
        {
            Default = true,
            DeviceDefault = true,
            OSPowerMode = OSPowerMode.BetterPerformance,
            CPUBoostLevel = CPUBoostLevel.Enabled,
            Guid = BetterPerformanceGuid,
            TDPOverrideEnabled = true,
            TDPOverrideValues = new[] { 25.0d, 25.0d, 25.0d },
            IntelEnduranceGamingEnabled = true,
            IntelEnduranceGamingPreset = (int)ctl_3d_endurance_gaming_mode_t.PERFORMANCE
        });

        // Max Performance
        DevicePowerProfiles.Add(new(Properties.Resources.PowerProfileOneXPlayerX1IntelBestPerformance, Properties.Resources.PowerProfileOneXPlayerX1IntelBestPerformanceDesc)
        {
            Default = true,
            DeviceDefault = true,
            OSPowerMode = OSPowerMode.BestPerformance,
            CPUBoostLevel = CPUBoostLevel.Enabled,
            Guid = BestPerformanceGuid,
            TDPOverrideEnabled = true,
            TDPOverrideValues = new[] { 35.0d, 35.0d, 35.0d },
            EPPOverrideEnabled = true,
            EPPOverrideValue = 32,
            IntelEnduranceGamingEnabled = false,
            IntelEnduranceGamingPreset = (int)ctl_3d_endurance_gaming_mode_t.PERFORMANCE,
        });

        // X2 shares the X1's controllers, but needs the Gen2 intercept-enable (sent after a delay)
        // to surface the M1/M2 back paddles on the vendor HID channel.
        VendorHidInitProfile = OxpHidInitProfile.X2;

        // Turbo emits Ctrl+Win+Alt, but its modifier order and Ctrl side vary by
        // firmware. InputsManager recognizes that set order-independently and emits
        // OEM1; keep these raw variants silenced and expose OEM1 with an empty chord.
        OEMChords.RemoveAll(c => c.state.Buttons.Contains(ButtonFlags.OEM1));
        OEMChords.Add(new KeyboardChord("Turbo",
            [KeyCode.RControlKey, KeyCode.LWin, KeyCode.LMenu],
            [KeyCode.LMenu, KeyCode.LWin, KeyCode.RControlKey],
            true, ButtonFlags.OEM1, flushInterval: 100));
        OEMChords.Add(new KeyboardChord("Turbo",
            [KeyCode.LControlKey, KeyCode.LWin, KeyCode.LMenu],
            [KeyCode.LMenu, KeyCode.LWin, KeyCode.LControlKey],
            true, ButtonFlags.OEM1, flushInterval: 100));
        OEMChords.Add(new KeyboardChord("Turbo", null, null, false, ButtonFlags.OEM1));

        // The Home button (vendor id 0x21 -> OEM3) fires via the vendor HID monitor and sends no
        // keyboard combo, so declare it with an empty chord purely so it appears in the OEM mapping UI.
        OEMChords.Add(new KeyboardChord("Home", null, null, false, ButtonFlags.OEM3));

        // After the EC take-over the KB button emits the firmware combo LCtrl+LWin+RCtrl+O, which
        // triggers the Windows on-screen keyboard (and the Win flickers Start). OEM2 itself is
        // delivered by the vendor HID (0x24), so replace the inherited X1 chord (wrong keys for the
        // X2) with a SILENCED chord matching the real combo to swallow the OS shortcut, and
        // re-declare OEM2 with an empty chord so it still shows as mappable.
        OEMChords.RemoveAll(c => c.state.Buttons.Contains(ButtonFlags.OEM2));
        // Larger flushInterval keeps the buffer captured longer so rapid/double presses (whose
        // sequences overlap) stay suppressed instead of leaking Win+O to the OS.
        OEMChords.Add(new KeyboardChord("Keyboard",
            [KeyCode.LControlKey, KeyCode.LWin, KeyCode.RControlKey, KeyCode.O],
            [KeyCode.LControlKey, KeyCode.LWin, KeyCode.RControlKey, KeyCode.O],
            true, ButtonFlags.OEM2, flushInterval: 300));
        OEMChords.Add(new KeyboardChord("Keyboard", null, null, false, ButtonFlags.OEM2));

        // OEM2 is delivered independently by the vendor HID and must remain available
        // for user mappings. Do not inherit the X1's default on-screen-keyboard action.
        DeviceHotkeys[typeof(OnScreenKeyboardCommands)].inputsChord.ButtonState[ButtonFlags.OEM2] = false;
    }

    protected override ButtonFlags MapVendorButton(byte buttonId)
    {
        return buttonId switch
        {
            0x20 => ButtonFlags.OEM1,
            0x21 => ButtonFlags.OEM3,
            0x22 => ButtonFlags.L4,   // M1 (left back paddle)
            0x23 => ButtonFlags.R4,   // M2 (right back paddle)
            0x24 => ButtonFlags.OEM2,
            _ => base.MapVendorButton(buttonId),
        };
    }

    protected override void SetTurboButtonTakeover(bool enabled)
    {
        // The X2 firmware exposes its EC through the SuRwECRegInterface ACPI/WMI
        // provider. WinRing0 port I/O (used by older OXP models) cannot access this
        // register on the X2, which is why takeover previously worked only after
        // OneXConsole had initialized it.
        try
        {
            using ManagementObjectSearcher searcher = new(
                "root\\WMI", "SELECT * FROM SuRwECRegInterface");
            using ManagementObjectCollection instances = searcher.Get();
            using ManagementObject? instance = instances.Cast<ManagementObject>().FirstOrDefault();

            if (instance is null)
                throw new InvalidOperationException("SuRwECRegInterface is unavailable");

            // SuRwECRegInterface packs its fields little-endian: group in bits
            // 0..7, offset in bits 8..15, and value in bits 16..23. Thus the
            // X2 takeover write is 0x40EB04 (value 0x40, offset 0xEB, group 4).
            byte value = enabled ? TurboTakeoverMask : (byte)0x00;
            byte group = (byte)(TurboTakeoverRegister >> 8);
            byte offset = (byte)(TurboTakeoverRegister & 0xFF);
            uint groupOffsetValue = group | ((uint)offset << 8) | ((uint)value << 16);
            // Use the positional overload. The .NET 10 System.Management package
            // throws NotFound while resolving this firmware provider's method
            // parameter class, even though the method itself is callable. The
            // positional call matches the provider's two parameters directly.
            object?[] arguments = [groupOffsetValue, null];
            instance.InvokeMethod("WriteECReg", arguments);

            LogManager.LogInformation(
                "{0} {1} OEM button through X2 WMI EC interface",
                enabled ? "Unlocked" : "Locked", ButtonFlags.OEM1);
        }
        catch (Exception ex)
        {
            LogManager.LogWarning(
                "Failed to {0} {1} OEM button through X2 WMI EC interface: {2}",
                enabled ? "unlock" : "lock", ButtonFlags.OEM1, ex.Message);
        }
    }

    protected override void VendorHidMonitor_ButtonChanged(byte buttonId, bool pressed)
    {
        ButtonFlags button = buttonId switch
        {
            0x20 => ButtonFlags.OEM1,
            0x21 => ButtonFlags.OEM3,
            0x24 => ButtonFlags.OEM2,
            _ => ButtonFlags.None,
        };

        if (button != ButtonFlags.None)
        {
            // These vendor-HID press/release reports can be shorter than an input
            // update. Emit a deterministic pulse for reliable mappings.
            if (pressed)
                KeyPressAndRelease(button, 100);
            return;
        }

        // Preserve the PR's original behavior for the paddles and unknown IDs.
        base.VendorHidMonitor_ButtonChanged(buttonId, pressed);
    }

    public override string GetGlyph(ButtonFlags button)
    {
        switch (button)
        {
            case ButtonFlags.OEM3:
                return "\u2219";
            case ButtonFlags.OEM1:
                return "\u2211";
            case ButtonFlags.OEM2:
                return "\u2210";
        }

        return base.GetGlyph(button);
    }

    // Battery protection (charge limit / bypass charging) is inherited from the X1
    // family, which writes to EC 0x4A3 (limit) / 0x4A4 (bypass) and gates on the
    // reported EC version. This X2 unit reports a *different* EC firmware line
    // (major 0 / minor 11) than the X1 Intel (which gates on minor >= 67), so the
    // X1 threshold is meaningless here and the 0x4A3/0x4A4 addresses are UNVERIFIED
    // on the X2's EC. To avoid writing to an unknown EC register, keep battery
    // protection disabled (base returns false) until the addresses are confirmed on
    // real X2 hardware. Flip this to a proper version gate once verified.
    public override bool IsBatteryProtectionSupported(int majorVersion, int minorVersion)
    {
        return false;
    }

    // Fan control: ECDetails (AddressFanControl=0x44A, AddressFanDuty=0x44B,
    // AddressStatusCommandPort=0x4E, AddressDataPort=0x4F, FanValueMin=0,
    // FanValueMax=184) are inherited from OneXPlayerX1. These are the standard OXP
    // EC fan registers and are expected to match on the X2 (same ONE-NETBOOK EC
    // family), but they have NOT been read-back verified on this X2 unit and need
    // real-world confirmation before relying on manual fan curves.
    //
    // Gyro/accelerometer matrices and LED presets/serial-LED protocol are also
    // inherited from OneXPlayerX1 and left unchanged (no evidence they differ on X2).
}
