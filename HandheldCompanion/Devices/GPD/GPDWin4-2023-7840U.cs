using HandheldCompanion.Inputs;
using HandheldCompanion.Managers;
using System;
using System.Collections.Generic;
using System.Numerics;
using WindowsInput.Events;

namespace HandheldCompanion.Devices;

public class GPDWin4_2023_7840U : IDevice
{
    public GPDWin4_2023_7840U()
    {
        // device specific settings
        ProductIllustration = "device_gpd4";

        // https://www.amd.com/en/products/apu/amd-ryzen-7-7840u
        nTDP = new double[] { 15, 15, 28 };
        cTDP = new double[] { 5, 28 };
        GfxClock = new double[] { 200, 2700 };

        AngularVelocityAxis = new Vector3(1.0f, 1.0f, -1.0f);
        AngularVelocityAxisSwap = new SortedDictionary<char, char>
        {
            { 'X', 'Y' },
            { 'Y', 'Z' },
            { 'Z', 'X' }
        };

        AccelerationAxis = new Vector3(1.0f, 1.0f, 1.0f);
        AccelerationAxisSwap = new SortedDictionary<char, char>
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

    public override void Close()
    {
        base.Close();
    }
}
