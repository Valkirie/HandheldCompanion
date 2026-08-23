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

    // CPU package temperature (°C) exposed by the EC. LibreHardwareMonitor cannot read
    // the MSR temperatures on the X2's Panther Lake CPU, so ReadCPUTemperature() feeds
    // this register into the sensor pipeline as a fallback (see LibreHardwarePlatform),
    // which drives the fan curve exactly like the LHM-read CPUs on other OneXPlayers.
    // Identified by probing the EC under load: it tracks CPU load directly and recovers
    // on cooldown, unlike the neighbouring board/SSD sensors.
    private const ushort CPUTemperatureRegister = 0x0470;

    public OneXPlayerX2()
    {
        // device specific settings
        ProductIllustration = "device_onexplayer_x2";
        ProductModel = "ONEXPLAYERX2";

        nTDP = new double[] { 25, 25, 35 };
        cTDP = new double[] { 3, 35 };
        GfxClock = new double[] { 100, 2300 };
        CpuClock = 4700;

        ECDetails = new ECDetails
        {
            AddressFanControl = 0x44A,
            AddressFanDuty = 0x44B,
            AddressStatusCommandPort = 0x4E,
            AddressDataPort = 0x4F,
            FanValueMin = 0,
            // The X2 EC rejects PWM values above ~184 (same ceiling as the X1) and
            // turns the fan off, so cap the usable range here rather than at 255.
            FanValueMax = 184
        };

        Capabilities |= DeviceCapabilities.FanControl;

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
        // X2 variants can expose the same vendor collection under either PID.
        // Keep both entries aligned with the HHD X1-mini vendor HID selector:
        // usage page 0xFF00, usage 0x0001.
        hidFilters[0x1305] = new HidFilter(unchecked((short)0xFF00), unchecked(0x0001));

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

    public override void SetFanControl(bool enable, int mode = 0)
    {
        // The X2 EC is only reachable through the SuRwECRegInterface WMI provider.
        // WinRing0 port I/O (EcWriteByte / ECRamDirectWriteByte) cannot access it,
        // which is why the fan register lives in the banked space (0x044A) like the
        // turbo takeover register (0x04EB). Writing the legacy ACPI offset (0x4A)
        // through WinRing0 silently does nothing on this hardware.
        byte value = enable ? (byte)FanControlMode.Manual : (byte)FanControlMode.Automatic;
        WriteFanRegister(ECDetails.AddressFanControl, value);
    }

    public override void SetFanDuty(double percent)
    {
        double clampedPercent = Math.Clamp(percent, 0.0d, 100.0d);
        double scaled = clampedPercent * (ECDetails.FanValueMax - ECDetails.FanValueMin) / 100.0d + ECDetails.FanValueMin;
        byte duty = (byte)Math.Round(scaled);
        WriteFanRegister(ECDetails.AddressFanDuty, duty);
    }

    public override float ReadFanDuty()
    {
        try
        {
            using OneXPlayerWmiEc ec = new();
            return ec.ReadByte(ECDetails.AddressFanDuty);
        }
        catch (Exception ex)
        {
            LogManager.LogWarning("Failed to read fan duty through X2 WMI EC interface: {0}", ex.Message);
            return 0;
        }
    }

    public override float? ReadCPUTemperature()
    {
        try
        {
            using OneXPlayerWmiEc ec = new();
            byte value = ec.ReadByte(CPUTemperatureRegister);

            // Reject obviously invalid readings (EC not ready / out of range) so the fan
            // curve falls back to its default rather than acting on a bogus temperature.
            if (value == 0 || value > 110)
                return null;

            return value;
        }
        catch (Exception ex)
        {
            LogManager.LogWarning("Failed to read CPU temperature through X2 WMI EC interface: {0}", ex.Message);
            return null;
        }
    }

    private void WriteFanRegister(ushort register, byte value)
    {
        try
        {
            using OneXPlayerWmiEc ec = new();
            ec.WriteByte(register, value);
        }
        catch (Exception ex)
        {
            LogManager.LogWarning("Failed to write fan register 0x{0:X3} through X2 WMI EC interface: {1}", register, ex.Message);
        }
    }

    protected override void InitializeVendorHidCommands()
    {
        // Wait for the vendor HID interface to be ready after enumeration, then send
        // only the two remap pages to expose the M1/M2 paddles and OEM buttons.
        // Do NOT send the vendor 0xB2 command: on the X2 it puts the pad firmware into
        // intercept mode and kills its XInput gamepad reports until a power-cycle.
        Thread.Sleep(4000);

        WriteVendorHidCommand(0xB4, BuildRemapPage1(0x01));
        Thread.Sleep(50);

        WriteVendorHidCommand(0xB4, BuildRemapPage2(0x01, 0x67, 0x66));
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
