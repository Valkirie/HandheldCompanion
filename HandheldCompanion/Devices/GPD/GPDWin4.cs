using HandheldCompanion.Inputs;
using HandheldCompanion.Managers;
using System;
using System.Collections.Generic;
using System.Numerics;
using WindowsInput.Events;

namespace HandheldCompanion.Devices;

public class GPDWin4 : IDevice
{
    public GPDWin4()
    {
        // device specific settings
        ProductIllustration = "device_gpd4";

        // https://www.amd.com/fr/products/apu/amd-ryzen-7-6800u
        nTDP = new double[] { 15, 15, 28 };
        cTDP = new double[] { 5, 28 };
        GfxClock = new double[] { 100, 2200 };
        CpuClock = 4700;

        // device specific capacities
        Capabilities = DeviceCapabilities.FanControl;

        ECDetails = new ECDetails
        {
            AddressFanControl = 0xC311,
            AddressFanDuty = 0xC880,
            AddressStatusCommandPort = 0x2E,
            AddressDataPort = 0x2F,
            FanValueMin = 0,
            FanValueMax = 127
        };

        GyrometerAxis = new Vector3(-1.0f, -1.0f, 1.0f);
        GyrometerAxisSwap = new SortedDictionary<char, char>
        {
            { 'X', 'X' },
            { 'Y', 'Z' },
            { 'Z', 'Y' }
        };

        AccelerometerAxisSwap = new SortedDictionary<char, char>
        {
            { 'X', 'X' },
            { 'Y', 'Z' },
            { 'Z', 'Y' }
        };

        // Note, OEM1 not configured as this device has it's own Menu button for guide button

        // Note, chords need to be manually configured in GPD app first by end user

        // GPD Back buttons do not have a "hold", configured buttons are key down and up immediately
        // Holding back buttons will result in same key down and up input every 2-3 seconds
        // Configured chords in GPD app need unique characters otherwise this leads to a
        // "mixed" result when pressing both buttons at the same time
        OEMChords.Add(new DeviceChord("Bottom button left",
            new List<KeyCode> { KeyCode.F11, KeyCode.L },
            new List<KeyCode> { KeyCode.F11, KeyCode.L },
            false, ButtonFlags.OEM2
        ));

        OEMChords.Add(new DeviceChord("Bottom button right",
            new List<KeyCode> { KeyCode.F12, KeyCode.R },
            new List<KeyCode> { KeyCode.F12, KeyCode.R },
            false, ButtonFlags.OEM3
        ));
    }

    public override string GetGlyph(ButtonFlags button)
    {
        switch (button)
        {
            case ButtonFlags.OEM2:
                return "\u220E";
            case ButtonFlags.OEM3:
                return "\u220F";
        }

        return defaultGlyph;
    }

    public override void SetFanControl(bool enable, int mode = 0)
    {
        switch (enable)
        {
            case false:
                base.SetFanDuty(0);
                return;
        }
    }

    public override void SetFanDuty(double percent)
    {
        if (ECDetails.AddressFanControl == 0)
            return;

        var duty = percent * (ECDetails.FanValueMax - ECDetails.FanValueMin) / 100 + ECDetails.FanValueMin;
        var data = Convert.ToByte(duty);

        ECRamDirectWrite(ECDetails.AddressFanControl, ECDetails, data);
    }

    public override bool Open()
    {
        var success = base.Open();
        if (!success)
            return false;

        // allow fan manipulation
        var EC_Chip_ID1 = ECRamReadByte(0x2000, ECDetails);
        if (EC_Chip_ID1 == 0x55)
        {
            var EC_Chip_Ver = ECRamReadByte(0x1060, ECDetails);
            EC_Chip_Ver = (byte)(EC_Chip_Ver | 0x80);

            LogManager.LogInformation("Unlocked GPD WIN 4 ({0}) fan control", EC_Chip_Ver);
            return ECRamDirectWrite(0x1060, ECDetails, EC_Chip_Ver);
        }

        return false;
    }

    public override void Close()
    {
        base.Close();
    }
}
