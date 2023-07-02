using System.Collections.Generic;
using ControllerCommon.Inputs;
using System.Numerics;
using ControllerCommon.Managers;
using WindowsInput.Events;

namespace ControllerCommon.Devices;

public class AynLoki : IDevice
{
    public AynLoki()
    {
        // Ayn Loki device generic settings
        ProductIllustration = "device_ayn_loki";
        ProductModel = "AynLoki";

        AngularVelocityAxis = new Vector3(1.0f, 1.0f, -1.0f);
        AngularVelocityAxisSwap = new SortedDictionary<char, char>
        {
            { 'X', 'Y' },
            { 'Y', 'Z' },
            { 'Z', 'X' }
        };

        AccelerationAxis = new Vector3(1.0f, -1.0f, -1.0f);
        AccelerationAxisSwap = new SortedDictionary<char, char>
        {
            { 'X', 'X' },
            { 'Y', 'Z' },
            { 'Z', 'Y' }
        };

        // device specific capacities
        /*
        Capacities = DeviceCapacities.FanControl;

        ECDetails = new ECDetails
        {
            AddressControl = 0x12C,
            AddressDuty = 0x11,
            AddressRegistry = 0x12,
            AddressData = 0x4F,
            ValueMin = 0,
            ValueMax = 128
        };

        */

        OEMChords.Add(new DeviceChord("Guide",
            new List<KeyCode> { KeyCode.LButton, KeyCode.XButton2 },
            new List<KeyCode> { KeyCode.LButton, KeyCode.XButton2 },
            false, ButtonFlags.OEM1
        ));

        OEMChords.Add(new DeviceChord("LCC",
            new List<KeyCode> { KeyCode.LControl, KeyCode.LShift, KeyCode.LMenu, KeyCode.T },
            new List<KeyCode> { KeyCode.T, KeyCode.LMenu, KeyCode.LShift, KeyCode.LControl },
            false, ButtonFlags.OEM2
        ));
    }
}