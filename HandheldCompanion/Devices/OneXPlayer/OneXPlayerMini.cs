using HandheldCompanion.Commands.Functions.HC;
using HandheldCompanion.Commands.Functions.Windows;
using HandheldCompanion.Inputs;
using HandheldCompanion.Shared;
using System;
using System.Collections.Generic;
using System.Numerics;
using WindowsInput.Events;

namespace HandheldCompanion.Devices;

public class OneXPlayerMini : OneXAOKZOE
{
    public OneXPlayerMini()
    {
        // device specific settings
        ProductIllustration = "device_onexplayer_mini";
        ProductModel = "ONEXPLAYERMini";

        GyrometerAxis = new Vector3(1.0f, -1.0f, 1.0f);
        GyrometerAxisSwap = new SortedDictionary<char, char>
        {
            { 'X', 'X' },
            { 'Y', 'Z' },
            { 'Z', 'Y' }
        };

        AccelerometerAxis = new Vector3(-1.0f, -1.0f, 1.0f);
        AccelerometerAxisSwap = new SortedDictionary<char, char>
        {
            { 'X', 'X' },
            { 'Y', 'Z' },
            { 'Z', 'Y' }
        };

        ECDetails = new ECDetails
        {
            AddressFanControl = 0x44A,
            AddressFanDuty = 0x44B,
            AddressStatusCommandPort = 0x4E,
            AddressDataPort = 0x4F,
            FanValueMin = 0,
            FanValueMax = 255
        };

        ACPI_FanMode_Address = 0xC4;           // Define the ACPI memory address for fan control mode
        ACPI_FanPWMDutyCycle_Address = 0xC5;   // Fan control PWM value

        OEMChords.Add(new KeyboardChord("Orange",
            [KeyCode.LControl, KeyCode.LWin, KeyCode.LMenu],
            [KeyCode.LControl, KeyCode.LWin, KeyCode.LMenu],
            false, ButtonFlags.OEM1
        ));

        OEMChords.Add(new KeyboardChord("Keyboard",
            [KeyCode.LWin, KeyCode.RControlKey, KeyCode.O],
            [KeyCode.O, KeyCode.RControlKey, KeyCode.LWin],
            false, ButtonFlags.OEM2
        ));

        OEMChords.Add(new KeyboardChord("Function",
            [KeyCode.LWin, KeyCode.D],
            [KeyCode.LWin, KeyCode.D],
            false, ButtonFlags.OEM3
        ));

        // prepare hotkeys
        DeviceHotkeys[typeof(MainWindowCommands)].inputsChord.ButtonState[ButtonFlags.OEM1] = true;
        DeviceHotkeys[typeof(QuickToolsCommands)].inputsChord.ButtonState[ButtonFlags.OEM3] = true;
        DeviceHotkeys[typeof(OnScreenKeyboardCommands)].inputsChord.ButtonState[ButtonFlags.OEM2] = true;
    }

    public override void SetFanControl(bool enable, int mode)
    {
        if (!UseOpenLib || !IsOpen)
            return;

        // Determine the fan control mode based enable
        byte controlValue = enable ? (byte)FanControlMode.Manual : (byte)FanControlMode.Automatic;

        // Update the fan control mode
        EcWriteByte(ACPI_FanMode_Address, controlValue);
    }

    public override void SetFanDuty(double percent)
    {
        if (!UseOpenLib || !IsOpen)
            return;

        // Convert 0-100 percentage to range
        byte fanSpeedSetpoint = (byte)(percent * (ECDetails.FanValueMax - ECDetails.FanValueMin) / 100 + ECDetails.FanValueMin);

        // Ensure the value is within the valid range
        fanSpeedSetpoint = Math.Min((byte)ECDetails.FanValueMax, Math.Max((byte)ECDetails.FanValueMin, fanSpeedSetpoint));

        // Set the requested fan speed
        EcWriteByte(ACPI_FanPWMDutyCycle_Address, fanSpeedSetpoint);
    }

    public override string GetGlyph(ButtonFlags button)
    {
        switch (button)
        {
            case ButtonFlags.OEM1:
                return "\u2219";
            case ButtonFlags.OEM2:
                return "\u2210";
            case ButtonFlags.OEM3:
                return "\u2218";
        }

        return defaultGlyph;
    }

    public override bool Open()
    {
        bool success = base.Open();
        if (!success)
            return false;

        // allow OneX button to pass key inputs
        EcWriteByte(0x1E, 0x01);
        if (EcReadByte(0x1E) == 0x01)
            LogManager.LogInformation("Unlocked {0} OEM button", ButtonFlags.OEM1);

        return success;
    }

    public override void Close()
    {
        EcWriteByte(0x1E, 0x00);
        if (EcReadByte(0x1E) == 0x00)
            LogManager.LogInformation("Locked {0} OEM button", ButtonFlags.OEM1);

        base.Close();
    }
}

public class OneXPlayerMiniAMD : OneXPlayerMini
{
    public OneXPlayerMiniAMD()
    {
        // https://www.amd.com/en/products/apu/amd-ryzen-7-5800u
        nTDP = new double[] { 15, 15, 20 };
        cTDP = new double[] { 10, 25 };
        GfxClock = new double[] { 100, 2000 };
        CpuClock = 4400;

        AccelerometerAxis = new Vector3(1.0f, 1.0f, 1.0f);
    }
}

public class OneXPlayerMiniIntel : OneXPlayerMini
{
    public OneXPlayerMiniIntel()
    {
        // https://ark.intel.com/content/www/us/en/ark/products/226254/intel-core-i71260p-processor-18m-cache-up-to-4-70-ghz.html
        nTDP = new double[] { 28, 28, 64 };
        cTDP = new double[] { 20, 64 };
        GfxClock = new double[] { 100, 1400 };
        CpuClock = 4700;

        GyrometerAxis = new Vector3(1.0f, -1.0f, -1.0f);
        GyrometerAxisSwap = new SortedDictionary<char, char>
        {
            { 'X', 'Y' },
            { 'Y', 'Z' },
            { 'Z', 'X' }
        };

        AccelerometerAxis = new Vector3(1.0f, -1.0f, -1.0f);
    }
}