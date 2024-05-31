﻿using HandheldCompanion.Inputs;
using HandheldCompanion.Managers;
using System.Collections.Generic;
using System.Numerics;
using WindowsInput.Events;

namespace HandheldCompanion.Devices;

public class OneXPlayerX1 : IDevice
{
    public OneXPlayerX1()
    {
        // device specific settings
        ProductIllustration = "device_onexplayer_x1";
        ProductModel = "ONEXPLAYERX1";
        
        GyrometerAxis = new Vector3(1.0f, -1.0f, 1.0f);
        GyrometerAxisSwap = new SortedDictionary<char, char>
        {
            { 'X', 'X' },
            { 'Y', 'Z' },
            { 'Z', 'Y' },
        };

        AccelerometerAxis = new Vector3(1.0f, -1.0f, -1.0f);
        AccelerometerAxisSwap = new SortedDictionary<char, char>
        {
            { 'X', 'X' },
            { 'Y', 'Z' },
            { 'Z', 'Y' },
        };
        
        // device specific capacities
        Capabilities = DeviceCapabilities.FanControl;

        ECDetails = new ECDetails
        {
            AddressFanControl = 0x44A,
            AddressFanDuty = 0x44B,
            AddressStatusCommandPort = 0x4E,
            AddressDataPort = 0x4F,
            FanValueMin = 0,
            FanValueMax = 184
        };

        OEMChords.Add(new DeviceChord("Turbo",
            new List<KeyCode> { KeyCode.RControlKey, KeyCode.LWin, KeyCode.LMenu },
            new List<KeyCode> { KeyCode.LMenu, KeyCode.LWin, KeyCode.RControlKey },
            false, ButtonFlags.OEM1
            ));
    }

    public override string GetGlyph(ButtonFlags button)
    {
        switch (button)
        {
            case ButtonFlags.OEM1:
                return "\u2211";
        }

        return defaultGlyph;
    }

    public override bool Open()
    {
        var success = base.Open();
        if (!success)
            return false;

        // allow OneX button to pass key inputs
        LogManager.LogInformation("Unlocked {0} OEM button", ButtonFlags.OEM1);

        ECRamDirectWrite(0x4EB, ECDetails, 0x40);

        return ECRamReadByte(0x4EB, ECDetails) == 0x40;
    }

    public override void Close()
    {
        LogManager.LogInformation("Locked {0} OEM button", ButtonFlags.OEM1);
        ECRamDirectWrite(0x4EB, ECDetails, 0x00);
        base.Close();
    }
}
