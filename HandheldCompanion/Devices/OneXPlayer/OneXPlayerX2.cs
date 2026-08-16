using HandheldCompanion.Commands.Functions.Windows;
using HandheldCompanion.Controllers;
using HandheldCompanion.Inputs;
using HandheldCompanion.Managers;
using HandheldCompanion.Misc;
using HandheldCompanion.Shared;
using System;
using System.Linq;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using WindowsInput.Events;
using YamlDotNet.Core.Tokens;
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

        nTDP = new double[] { 25, 25, 35 };
        cTDP = new double[] { 3, 35 };
        GfxClock = new double[] { 100, 2300 };
        CpuClock = 4700;

        DevicePowerProfiles.Add(new(Properties.Resources.PowerProfileOneXPlayerX1IntelBetterBattery, Properties.Resources.PowerProfileOneXPlayerX1IntelBetterBatteryDesc)
        {
            Default = true,
            DeviceDefault = true,
            OSPowerMode = OSPowerMode.BetterBattery,
            CPUBoostLevel = CPUBoostLevel.Disabled,
            Guid = BetterBatteryGuid,
            TDPOverrideEnabled = true,
            TDPOverrideValues = new[] { 15.0d, 15.0d, 15.0d },
            IntelEnduranceGamingEnabled = true,
            IntelEnduranceGamingPreset = (int)ctl_3d_endurance_gaming_mode_t.MAX
        });

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

        vendorId = 0x1A86;
        productIds = [0xFE00, 0x1305];

        // Suppress both firmware chord variants; OEM1 is delivered over vendor HID.
        OEMChords.RemoveAll(c => c.state.Buttons.Contains(ButtonFlags.OEM1));
        OEMChords.Add(new KeyboardChord("Turbo",
            [KeyCode.RControlKey, KeyCode.LWin, KeyCode.LMenu],
            [KeyCode.LMenu, KeyCode.LWin, KeyCode.RControlKey],
            false, ButtonFlags.OEM1, flushInterval: 100, orderIndependent: true));
        OEMChords.Add(new KeyboardChord("Turbo",
            [KeyCode.LControlKey, KeyCode.LWin, KeyCode.LMenu],
            [KeyCode.LMenu, KeyCode.LWin, KeyCode.LControlKey],
            false, ButtonFlags.OEM1, flushInterval: 100, orderIndependent: true));

        // Vendor-only buttons use empty chords so they remain visible in the mapping UI.
        OEMChords.Add(new KeyboardChord("Home", null, null, false, ButtonFlags.OEM3));

        // Suppress the X2 keyboard shortcut; OEM2 is delivered over vendor HID.
        OEMChords.RemoveAll(c => c.state.Buttons.Contains(ButtonFlags.OEM2));
        OEMChords.Add(new KeyboardChord("Keyboard",
            [KeyCode.LControlKey, KeyCode.LWin, KeyCode.RControlKey, KeyCode.O],
            [KeyCode.LControlKey, KeyCode.LWin, KeyCode.RControlKey, KeyCode.O],
            true, ButtonFlags.OEM2, flushInterval: 300));
        OEMChords.Add(new KeyboardChord("Keyboard", null, null, false, ButtonFlags.OEM2));

        // X2 OEM2 is remappable and has no default keyboard action.
        DeviceHotkeys[typeof(OnScreenKeyboardCommands)].inputsChord.ButtonState[ButtonFlags.OEM2] = false;
    }

    protected override async void Device_Inserted(bool reScan = false)
    {
        if (reScan)
            await WaitUntilReady();

        base.Device_Inserted();
        await Task.Delay(4000);
        WriteVendorHidCommand(0xB2, [0x01, 0x1F, 0x40, 0x03, 0x02, 0x03, 0x00, 0x00, 0x00, 0x01]);
    }

    public override XInputController? CreateController(PnPDetails details)
    {
        return new OneXPlayerX2Controller(details);
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
            using (OneXPlayerWmiEc ec = new())
            {
                byte currentValue = ec.ReadByte(TurboTakeoverRegister);
                byte value = enabled ? (byte)(currentValue | TurboTakeoverMask) : (byte)(currentValue & ~TurboTakeoverMask);

                ec.WriteByte(TurboTakeoverRegister, value);

                // wait a bit for the EC to process the change
                Thread.Sleep(50);

                byte actualValue = ec.ReadByte(TurboTakeoverRegister);
                if (actualValue == value)
                    LogManager.LogInformation("{0} {1} OEM button through X2 WMI EC interface", enabled ? "Unlocked" : "Locked", ButtonFlags.OEM1);
                else
                    LogManager.LogWarning("Failed to {0} OEM button through X2 WMI EC interface (expected 0x{1:X2}, actual 0x{2:X2})", enabled ? "unlock" : "lock", value, actualValue);
            }
        }
        catch (Exception ex)
        {
            LogManager.LogWarning("Failed to {0} {1} OEM button through X2 WMI EC interface: {2}", enabled ? "unlock" : "lock", ButtonFlags.OEM1, ex.Message);
        }
    }

    protected override void HandleEvent(byte buttonId, bool pressed)
    {
        ButtonFlags button = MapVendorButton(buttonId);

        switch (button)
        {
            case ButtonFlags.OEM1:
            case ButtonFlags.OEM2:
            case ButtonFlags.OEM3:
                if (pressed)
                    KeyPressAndRelease(button, 100);
                return;
        }

        base.HandleEvent(buttonId, pressed);
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

    // X1 battery-protection registers are not verified on X2 hardware.
    public override bool IsBatteryProtectionSupported(int majorVersion, int minorVersion)
    {
        return false;
    }
}
