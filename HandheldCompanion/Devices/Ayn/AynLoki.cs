using HandheldCompanion.Inputs;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Windows.Media;
using WindowsInput.Events;
using static HandheldCompanion.Utils.DeviceUtils;
namespace HandheldCompanion.Devices;

public class AynLoki : IDevice
{
    // Fan Control Mode.
    // 0 Manual Mode
    // 1 Automatic mode
    // 2 User Defined Mode
    private enum FanControlMode
    {
        Manual = 0,
        Automatic = 1,
        User = 2,
    }

    public AynLoki()
    {
        // Ayn Loki device generic settings
        ProductIllustration = "device_ayn_loki";
        ProductModel = "AynLoki";

        GyrometerAxis = new Vector3(1.0f, 1.0f, -1.0f);
        GyrometerAxisSwap = new SortedDictionary<char, char>
        {
            { 'X', 'Y' },
            { 'Y', 'Z' },
            { 'Z', 'X' }
        };

        AccelerometerAxis = new Vector3(1.0f, -1.0f, -1.0f);
        AccelerometerAxisSwap = new SortedDictionary<char, char>
        {
            { 'X', 'X' },
            { 'Y', 'Z' },
            { 'Z', 'Y' }
        };

        // device specific capacities        
        Capabilities |= DeviceCapabilities.FanControl;
        Capabilities |= DeviceCapabilities.DynamicLighting;
        DynamicLightingCapabilities |= LEDLevel.SolidColor;

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

    public override void SetFanControl(bool enable, int mode)
    {
        //LogManager.LogDebug("AynLoki Set Fan Control {0}", enable);

        // Define the ACPI memory address for fan control mode
        byte ACPI_FanMode_Address = 0x10;

        // Determine the fan control mode based enable
        byte controlValue = enable ? (byte)FanControlMode.Manual : (byte)FanControlMode.Automatic;

        // Update the fan control mode
        ECRAMWrite(ACPI_FanMode_Address, controlValue);
    }

    public override void SetFanDuty(double percent)
    {
        //LogManager.LogDebug("AynLoki Set Fan Control Speed {0}%", percent);

        // Fan control PWM value, range 0-128 (0 speed - 128 speed max)
        byte ACPI_FanPWMDutyCycle_Address = 0x11;

        // Convert 0-100 percentage to 0-128 range
        byte fanSpeedSetpoint = (byte)(percent * 1.28);

        // Ensure the value is within the valid range
        fanSpeedSetpoint = Math.Min((byte)128, Math.Max((byte)0, fanSpeedSetpoint));

        // Set the requested fan speed
        ECRAMWrite(ACPI_FanPWMDutyCycle_Address, fanSpeedSetpoint);
    }

    public override float ReadFanDuty()
    {
        // Todo, untested and unverified

        // Define ACPI memory addresses for fan speed data
        byte ACPI_FanSpeed_5_Address = 0x20;
        byte ACPI_FanTempe_5_Address = 0x21;

        // Initialize the fan speed percentage
        int fanSpeedPercentageActual = 0;

        // Read the two bytes from memory (assumed to represent fan speed)
        uint data1 = ECRamReadByte(ACPI_FanSpeed_5_Address);
        uint data2 = ECRamReadByte(ACPI_FanTempe_5_Address);

        //LogManager.LogDebug("AynLoki ReadFanDuty data1 {0} data2 {1}", data1, data2);

        // Combine the two bytes into a 16-bit integer (fanSpeed)
        short fanSpeed = (short)((data2 << 8) | data1);

        // Assign the fan speed as a percentage to fanSpeedPercentageActual
        fanSpeedPercentageActual = fanSpeed;

        //LogManager.LogDebug("AynLoki ReadFanDuty percentage actual {0}", fanSpeedPercentageActual);

        return fanSpeedPercentageActual;
    }

    public override bool SetLedColor(Color MainColor, Color SecondaryColor, LEDLevel level, int speed)
    {
        //LogManager.LogDebug("AynLoki Set LED color");

        // Set LED color
        byte PWM_R_Address = 0xB0; // PWM Red Duty cycle, range 0x00-0xFF
        byte PWM_G_Address = 0xB1; // PWM Green Duty cycle, range 0x00-0xFF
        byte PWM_B_Address = 0xB2; // PWM Blue Duty cycle, range 0x00-0xFF
        byte LED_Control_mode_Address = 0xB3;
        byte LED_Control_CompletedValue = 0x00;
        byte LED_Control_Save = 0xAA; // Update request
        byte LED_Control_RGB_Idle = 0x55; // This is in Ayn example code, not used in HC.

        /*
        0x00, EC writes 0x00 to notify Host that the operation has been completed

        0xAA, Host writes 0xAA to inform EC that PWM_RGB needs to be updated.

        Interaction logic: when 0xB3 is 0, Host can update the three values of PWM_RGB, 
        after updating, write 0xB3 to 0xAA to inform EC, after EC finished operation, 
        write 0xB3 to 0x01, Host write 0x01, that is, enter into auto-breathing.
        */

        // Todo, this ec write might not be required, code example and documentation from Ayn conflict
        ECRAMWrite(LED_Control_mode_Address, LED_Control_Save);

        uint LED_Control_Mode_Value = ECRamReadByte(LED_Control_mode_Address);

        if (LED_Control_Mode_Value == LED_Control_CompletedValue)
        {
            // Update RGB addresses with respective values
            ECRAMWrite(PWM_R_Address, MainColor.R);
            ECRAMWrite(PWM_G_Address, MainColor.G);
            ECRAMWrite(PWM_B_Address, MainColor.B);

            ECRAMWrite(LED_Control_mode_Address, LED_Control_Save);

            //LogManager.LogDebug("AynLoki Set LED write memory done");
        }

        return true;
    }

    public override string GetGlyph(ButtonFlags button)
    {
        switch (button)
        {
            case ButtonFlags.OEM1:
                return "\u220C";
            case ButtonFlags.OEM2:
                return "\u220D";
        }

        return defaultGlyph;
    }
}