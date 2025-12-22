using HandheldCompanion.Inputs;
using System;
using System.Collections.Generic;
using System.Numerics;
using WindowsInput.Events;

namespace HandheldCompanion.Devices;

public class GPDWin5 : IDevice
{
    // EC (GPD Duo/Win5 map via 0x4E/0x4F)
    private const ushort EC_RPM_HI = 0x0478; // read
    private const ushort EC_RPM_LO = 0x0479; // read
    private const ushort EC_FAN_DUTY_1 = 0x047A; // write (0=auto)
    private const ushort EC_FAN_DUTY_2 = 0x047B; // write (0=auto)

    public GPDWin5()
    {
        ProductIllustration = "device_gpd5";
        UseOpenLib = true;

        // https://www.amd.com/en/products/processors/laptop/ryzen/ai-300-series/amd-ryzen-ai-max-385.html
        nTDP = new double[] { 55, 55, 75 };
        cTDP = new double[] { 8, 85 };

        // Todo: get exact processor names and use switch/case
        string Processor = MotherboardInfo.ProcessorName;
        if (Processor.Contains("385"))
        {
            GfxClock = new double[] { 500, 2800 };
            CpuClock = 5000;
        }
        else if (Processor.Contains("395"))
        {
            GfxClock = new double[] { 500, 2900 };
            CpuClock = 5100;
        }

        Capabilities = DeviceCapabilities.FanControl;

        // Win 5 uses the 4E/4F SIO window; fan value range is 0..244
        ECDetails = new ECDetails
        {
            AddressStatusCommandPort = 0x4E,
            AddressDataPort = 0x4F,
            AddressFanControl = 0x0000, // not used on this device
            AddressFanDuty = 0x0000,    // not used; we write both fans directly
            FanValueMin = 0,
            FanValueMax = 244
        };

        GyrometerAxis = new Vector3(1.0f, -1.0f, -1.0f);
        GyrometerAxisSwap = new SortedDictionary<char, char>
        {
            { 'X', 'Y' },
            { 'Y', 'Z' },
            { 'Z', 'X' }
        };

        AccelerometerAxis = new Vector3(-1.0f, -1.0f, 1.0f);
        AccelerometerAxisSwap = new SortedDictionary<char, char>
        {
            { 'X', 'X' },
            { 'Y', 'Z' },
            { 'Z', 'Y' }
        };

        // GPD Win 5 specific chords
        // todo: figure out which value is which button
        /*
         * F13 = 0x7C
         * F14 = 0x7D
         * F15 = 0x7E
        */

        OEMChords.Add(new KeyboardChord("Custom button 1", [KeyCode.F13], [KeyCode.F13], false, ButtonFlags.OEM2));
        OEMChords.Add(new KeyboardChord("Custom button 2", [KeyCode.F14], [KeyCode.F14], false, ButtonFlags.OEM3));
        OEMChords.Add(new KeyboardChord("Custom button 3", [KeyCode.F15], [KeyCode.F15], false, ButtonFlags.OEM4));
    }

    public override void SetFanControl(bool enable, int mode = 0)
    {
        // On Win 5, "auto" is simply duty 0 on both fan registers.
        if (!enable)
        {
            if (!UseOpenLib || !IsOpen) return;
            ECRamDirectWriteByte(EC_FAN_DUTY_1, ECDetails, 0x00);
            ECRamDirectWriteByte(EC_FAN_DUTY_2, ECDetails, 0x00);
            return;
        }
    }

    public override void SetFanDuty(double percent)
    {
        if (!UseOpenLib || !IsOpen)
            return;

        // Clamp and scale to 0..244 (Win 5 max)
        if (percent < 0) percent = 0;
        if (percent > 100) percent = 100;

        double dutyD = percent * (ECDetails.FanValueMax - ECDetails.FanValueMin) / 100.0 + ECDetails.FanValueMin;
        byte duty = Convert.ToByte(dutyD);

        // Manual duty for both fans
        ECRamDirectWriteByte(EC_FAN_DUTY_1, ECDetails, duty);
        ECRamDirectWriteByte(EC_FAN_DUTY_2, ECDetails, duty);
    }

    public override float ReadFanDuty()
    {
        if (!UseOpenLib || !IsOpen)
            return 0;

        byte hi = ECRamDirectReadByte(EC_RPM_HI, ECDetails);
        byte lo = ECRamDirectReadByte(EC_RPM_LO, ECDetails);
        return (hi << 8) | lo;
    }

    public bool GetOnlyTypeC()
    {
        if (!UseOpenLib || !IsOpen)
            return false;

        byte val = ECRamDirectReadByte(0x0577, ECDetails);
        return val == 0x02 || val == 0x10; // Device is running on Type-C only
    }
}
