using System.Collections.Generic;
using ControllerCommon.Inputs;
using ControllerCommon.Managers;
using WindowsInput.Events;

namespace ControllerCommon.Devices;

public class OneXPlayerMini : IDevice
{
    public OneXPlayerMini()
    {
        // device specific settings
        ProductIllustration = "device_onexplayer_mini";
        ProductModel = "ONEXPLAYERMini";

        AngularVelocityAxisSwap = new SortedDictionary<char, char>
        {
            { 'X', 'X' },
            { 'Y', 'Z' },
            { 'Z', 'Y' }
        };

        AccelerationAxisSwap = new SortedDictionary<char, char>
        {
            { 'X', 'X' },
            { 'Y', 'Z' },
            { 'Z', 'Y' }
        };

        // device specific capacities
        Capacities = DeviceCapacities.FanControl;

        ECDetails = new ECDetails
        {
            AddressControl = 0x44A,
            AddressDuty = 0x44B,
            AddressRegistry = 0x4E,
            AddressData = 0x4F,
            ValueMin = 0,
            ValueMax = 255
        };

        OEMChords.Add(new DeviceChord("Orange",
            new List<KeyCode> { KeyCode.LControl, KeyCode.LWin, KeyCode.LMenu },
            new List<KeyCode> { KeyCode.LControl, KeyCode.LWin, KeyCode.LMenu },
            false, ButtonFlags.OEM1
        ));

        OEMChords.Add(new DeviceChord("Keyboard",
            new List<KeyCode> { KeyCode.LWin, KeyCode.RControlKey, KeyCode.O },
            new List<KeyCode> { KeyCode.O, KeyCode.RControlKey, KeyCode.LWin },
            false, ButtonFlags.OEM2
        ));

        OEMChords.Add(new DeviceChord("Function",
            new List<KeyCode> { KeyCode.LWin, KeyCode.D },
            new List<KeyCode> { KeyCode.LWin, KeyCode.D },
            false, ButtonFlags.OEM3
        ));
    }

    public override bool Open()
    {
        var success = base.Open();
        if (!success)
            return false;

        // allow OneX button to pass key inputs
        LogManager.LogInformation("Unlocked {0} OEM button", ButtonFlags.OEM1);
        return ECRamDirectWrite(0x41E, ECDetails, 0x01);
    }

    public override void Close()
    {
        LogManager.LogInformation("Locked {0} OEM button", ButtonFlags.OEM1);
        ECRamDirectWrite(0x41E, ECDetails, 0x00);
        base.Close();
    }
}