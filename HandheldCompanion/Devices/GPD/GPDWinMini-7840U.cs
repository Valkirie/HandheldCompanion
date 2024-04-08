using HandheldCompanion.Inputs;
using System.Collections.Generic;
using System.Numerics;
using WindowsInput.Events;

namespace HandheldCompanion.Devices;

public class GPDWinMini_7840U : IDevice
{
    public GPDWinMini_7840U()
    {
        // https://www.amd.com/en/products/apu/amd-ryzen-7-7840u
        nTDP = new double[] { 15, 15, 18 };
        cTDP = new double[] { 5, 18 };
        GfxClock = new double[] { 200, 2700 };
        CpuClock = 5100;

        // device specific settings
        ProductIllustration = "device_gpd_winmini";

        // device specific capacities
        Capabilities = DeviceCapabilities.FanControl;

        ECDetails = new ECDetails
        {
            AddressFanControl = 0x47A,          // Fan % setpoint address, 0 for off, 1 to 244 for 0 - 100%
            AddressFanDuty = 0x47A,             // Fan duty and control are the same for this device
            AddressStatusCommandPort = 0x4E,    // Unverified
            AddressDataPort = 0x4F,             // Unverified
            FanValueMin = 0,                    // 0 is off, but functions do + lowest value, so 0 required
            FanValueMax = 244                   // 100% ~6000 RPM
        };

        GyrometerAxis = new Vector3(1.0f, -1.0f, -1.0f);
        GyrometerAxisSwap = new SortedDictionary<char, char>
        {
            { 'X', 'Y' },
            { 'Y', 'Z' },
            { 'Z', 'X' }
        };

        AccelerometerAxis = new Vector3(-1.0f, 1.0f, 1.0f);
        AccelerometerAxisSwap = new SortedDictionary<char, char>
        {
            { 'X', 'X' },
            { 'Y', 'Z' },
            { 'Z', 'Y' }
        };

        // Disabled this one as Win Max 2 also sends an Xbox guide input when Menu key is pressed.
        OEMChords.Add(new DeviceChord("Menu",
            new List<KeyCode> { KeyCode.LButton | KeyCode.XButton2 },
            new List<KeyCode> { KeyCode.LButton | KeyCode.XButton2 },
            true, ButtonFlags.OEM1
        ));

        // note, need to manually configured in GPD app
        OEMChords.Add(new DeviceChord("L4",
            new List<KeyCode> { KeyCode.F11, KeyCode.L },
            new List<KeyCode> { KeyCode.F11, KeyCode.L },
            false, ButtonFlags.OEM2
        ));

        OEMChords.Add(new DeviceChord("R4",
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
                return "\u2276";
            case ButtonFlags.OEM3:
                return "\u2277";
        }

        return defaultGlyph;
    }

    public override float ReadFanDuty()
    {
        // Does not work, reads 255 255
        // Define memory addresses for fan speed data
        byte Address1 = 0x78;
        byte Address2 = 0x79;

        // Initialize the fan speed percentage
        int fanSpeedPercentageActual = 0;

        // Read the two bytes from memory (assumed to represent fan speed)
        uint data1 = ECRamReadByte(Address1);
        uint data2 = ECRamReadByte(Address2);

        //LogManager.LogDebug("ReadFanDuty data1 {0} data2 {1}", data1, data2);

        // Combine the two bytes into a 16-bit integer (fanSpeed)
        short fanSpeed = (short)((data2 << 8) | data1);

        // Assign the fan speed as a percentage to fanSpeedPercentageActual
        fanSpeedPercentageActual = fanSpeed;

        //LogManager.LogDebug("ReadFanDuty percentage actual {0}", fanSpeedPercentageActual);

        return fanSpeedPercentageActual;
    }
}