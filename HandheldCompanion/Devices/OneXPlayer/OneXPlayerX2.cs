using HandheldCompanion.Commands.Functions.HC;
using HandheldCompanion.Commands.Functions.Windows;
using HandheldCompanion.Controllers;
using HandheldCompanion.Inputs;
using HandheldCompanion.Shared;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WindowsInput.Events;
namespace HandheldCompanion.Devices;

public class OneXPlayerX2 : OneXPlayerX1
{
    private OneXPlayerWmiEc? _wmiEc;

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

    protected virtual bool UseWmiEc => true;

    public OneXPlayerX2()
    {
        // device specific settings
        ProductIllustration = "device_onexplayer_x2";
        ProductModel = "ONEXPLAYERX2";

        nTDP = new double[] { 25, 25, 35 };
        cTDP = new double[] { 3, 35 };
        GfxClock = new double[] { 100, 2300 };
        CpuClock = 4700;

        // IMU matrices are now loaded from OneXPlayerX2.json via IDevice.ApplyDeviceConfiguration()

        ECDetails = new ECDetails
        {
            AddressFanControl = 0x44A,
            AddressFanDuty = 0x44B,
            AddressStatusCommandPort = 0x4E,
            AddressDataPort = 0x4F,
            FanValueMin = 0,
            FanValueMax = 184
        };

        // The X2 does not have a serial port, so we disable it to avoid unnecessary errors in the logs.
        EnableSerialPort = false;

        // Override the default TDP values for each power profile to match the X2's presets.
        foreach (var profile in DevicePowerProfiles)
        {
            if (profile.Guid == BetterBatteryGuid)
                profile.TDPOverrideValues = new[] { 15.0d, 15.0d, 15.0d };
            else if (profile.Guid == BetterPerformanceGuid)
                profile.TDPOverrideValues = new[] { 25.0d, 25.0d, 25.0d };
            else if (profile.Guid == BestPerformanceGuid)
                profile.TDPOverrideValues = new[] { 35.0d, 35.0d, 35.0d };
        }

        vendorId = 0x1A86;
        productIds = [0xFE00, 0x1305];
        // X2 variants can expose the same vendor collection under either PID.
        // Keep both entries aligned with the HHD X1-mini vendor HID selector:
        // usage page 0xFF00, usage 0x0001.
        hidFilters[0x1305] = new HidFilter(unchecked((short)0xFF00), unchecked(0x0001));

        // Suppress both firmware chord variants; OEM1 is delivered over vendor HID.
        OEMChords.RemoveAll(c => c.state.Buttons.Contains(ButtonFlags.OEM1));
        // OXP sends keyUp events in random order
        OEMChords.Add(new KeyboardChord("Turbo", [KeyCode.LControl, KeyCode.LWin, KeyCode.LMenu], [KeyCode.LMenu, KeyCode.LWin, KeyCode.LControl], false, ButtonFlags.OEM1, flushInterval: 100));
        OEMChords.Add(new KeyboardChord("Turbo", [KeyCode.LControl, KeyCode.LWin, KeyCode.LMenu], [KeyCode.LMenu, KeyCode.LControl, KeyCode.LWin], false, ButtonFlags.OEM1, flushInterval: 100));
        OEMChords.Add(new KeyboardChord("Turbo", [KeyCode.LControl, KeyCode.LWin, KeyCode.LMenu], [KeyCode.LControl, KeyCode.LWin, KeyCode.LMenu], false, ButtonFlags.OEM1, flushInterval: 100));

        // Suppress the X2 keyboard shortcut; OEM2 is delivered over vendor HID.
        OEMChords.RemoveAll(c => c.state.Buttons.Contains(ButtonFlags.OEM2));
        OEMChords.Add(new KeyboardChord("Keyboard", [KeyCode.LControl, KeyCode.LWin, KeyCode.O], [KeyCode.O, KeyCode.LWin, KeyCode.LControl], false, ButtonFlags.OEM2, flushInterval: 100));

        // OEM buttons that does not emit keyboard events are still mapped to their respective chords for hotkey support.
        OEMChords.Add(new KeyboardChord("Home", null, null, false, ButtonFlags.OEM3));

        // Disabled this one as ONEX also sends an Xbox guide input when Menu key is pressed.
        OEMChords.RemoveAll(c => c.state.Buttons.Contains(ButtonFlags.OEM4));
        OEMChords.Add(new KeyboardChord("G-Key", [KeyCode.LButton, KeyCode.XButton2], [KeyCode.LButton, KeyCode.XButton2], true, ButtonFlags.OEM4));

        // override hotkeys triggers
        DeviceHotkeys[typeof(MainWindowCommands)].inputsChord.ButtonState[ButtonFlags.OEM3] = true;
        DeviceHotkeys[typeof(MainWindowCommands)].InputsChordType = InputsChordType.Click;
        DeviceHotkeys[typeof(QuickToolsCommands)].inputsChord.ButtonState[ButtonFlags.OEM1] = true;
        DeviceHotkeys[typeof(OnScreenKeyboardCommands)].inputsChord.ButtonState[ButtonFlags.OEM2] = true;
    }

    public override bool Open()
    {
        // The X2 firmware exposes its EC through the SuRwECRegInterface ACPI/WMI
        // provider. WinRing0 port I/O (used by older OXP models) cannot access this
        // register on the X2, which is why takeover previously worked only after
        // OneXConsole had initialized it.
        if (UseWmiEc)
        {
            try
            {
                lock (updateLock)
                {
                    _wmiEc = new OneXPlayerWmiEc();
                }
            }
            catch (Exception ex)
            {
                LogManager.LogWarning("Failed to open X2 WMI EC interface: {0}", ex.Message);
            }
        }

        return base.Open();
    }

    public override void Close()
    {
        if (UseWmiEc)
        {
            lock (updateLock)
            {
                _wmiEc?.Dispose();
                _wmiEc = null;
            }
        }

        base.Close();
    }

    protected override async Task ConfigureController()
    {
        WriteVendorHidCommand(0xB4, BuildRemapPage1(0x01));
        await Task.Delay(50);

        WriteVendorHidCommand(0xB4, BuildRemapPage2(0x01, 0x67, 0x66));
        await Task.Delay(50);

        WriteVendorHidCommand(0xB4, BuildRemapPage3(0x01));
        await Task.Delay(50);
    }

    protected virtual byte[] BuildRemapPage3(byte preset) =>
    [
        0x02, 0x38, 0x20, 0x03, preset,
        0x24, 0x02, 0x02, 0x05, 0x00, 0x00,
        0x25, 0x01, 0x21, 0x00, 0x00, 0x00,
    ];

    public override void SetFanControl(bool enable, int mode = 0)
    {
        byte value = enable ? (byte)FanControlMode.Manual : (byte)FanControlMode.Automatic;
        WriteFanRegister(ECDetails.AddressFanControl, value);

        // set flag
        hasAppliedSoftwareFanProfile = enable;
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
        lock (updateLock)
        {
            try
            {
                return _wmiEc?.ReadByte(ECDetails.AddressFanDuty) ?? 0;
            }
            catch (Exception ex)
            {
                LogManager.LogWarning("Failed to read fan duty through X2 WMI EC interface: {0}", ex.Message);
                return 0;
            }
        }
    }

    public override float? ReadCPUTemperature()
    {
        lock (updateLock)
        {
            try
            {
                // Reject obviously invalid readings (EC not ready / out of range) so the fan
                // curve falls back to its default rather than acting on a bogus temperature.
                byte value = _wmiEc?.ReadByte(CPUTemperatureRegister) ?? 0;
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
    }

    private void WriteFanRegister(ushort register, byte value)
    {
        lock (updateLock)
        {
            try
            {
                (_wmiEc ?? throw new InvalidOperationException("X2 WMI EC is not open")).WriteByte(register, value);
            }
            catch (Exception ex)
            {
                LogManager.LogWarning("Failed to write fan register 0x{0:X3} through X2 WMI EC interface: {1}", register, ex.Message);
            }
        }
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
            0x21 => ButtonFlags.OEM3, // HOME
            0x22 => ButtonFlags.L4,   // M1 (left back paddle)
            0x23 => ButtonFlags.R4,   // M2 (right back paddle)
            0x24 => ButtonFlags.OEM2, // KEYBOARD
            _ => base.MapVendorButton(buttonId),
        };
    }

    protected override void SetTurboButtonTakeover(bool enabled)
    {
        lock (updateLock)
        {
            try
            {
                OneXPlayerWmiEc ec = _wmiEc ?? throw new InvalidOperationException("X2 WMI EC is not open");

                // read the current register value, set or clear the takeover bit, and write it back
                byte currentValue = ec.ReadByte(TurboTakeoverRegister);
                byte value = enabled ? (byte)(currentValue | TurboTakeoverMask) : (byte)(currentValue & ~TurboTakeoverMask);
                ec.WriteByte(TurboTakeoverRegister, value);

                // wait a bit for the EC to process the change
                Thread.Sleep(50);

                // check that the register now contains the expected value
                byte actualValue = ec.ReadByte(TurboTakeoverRegister);
                if (actualValue == value)
                    LogManager.LogInformation("{0} {1} OEM button through X2 WMI EC interface", enabled ? "Unlocked" : "Locked", ButtonFlags.OEM1);
                else
                    LogManager.LogWarning("Failed to {0} OEM button through X2 WMI EC interface (expected 0x{1:X2}, actual 0x{2:X2})", enabled ? "unlock" : "lock", value, actualValue);
            }
            catch (Exception ex)
            {
                LogManager.LogWarning("Failed to {0} {1} OEM button through X2 WMI EC interface: {2}", enabled ? "unlock" : "lock", ButtonFlags.OEM1, ex.Message);
            }
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
            case ButtonFlags.OEM1:  // Turbo
                return "\u2211";
            case ButtonFlags.OEM2:  // Keyboard
                return "\u2210";
            case ButtonFlags.OEM3:  // Home
                return "\u221C";
            case ButtonFlags.OEM4:  // G-Key/Function
                return "\u2218";
        }

        return base.GetGlyph(button);
    }

    // X1 battery-protection registers are not verified on X2 hardware.
    public override bool IsBatteryProtectionSupported(int majorVersion, int minorVersion)
    {
        return false;
    }
}
