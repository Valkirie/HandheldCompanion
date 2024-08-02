using System;
using System.Collections.Generic;
using System.Numerics;

namespace HandheldCompanion.Devices;

public class OneXPlayerMiniIntel : OneXPlayerMini
{
    private enum FanControlMode
    {
        Manual = 0x88,
        Automatic = 0x00
    }

    // Define the ACPI memory address for fan control mode
    byte ACPI_FanMode_Address = 0xC4;
    // Fan control PWM value
    byte ACPI_FanPWMDutyCycle_Address = 0xC5;

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

    public override void SetFanControl(bool enable, int mode)
    {
        if (!IsOpen)
            return;

        // Determine the fan control mode based enable
        byte controlValue = enable ? (byte)FanControlMode.Manual : (byte)FanControlMode.Automatic;

        // Update the fan control mode
        if (!enable)
            ECRAMWrite(ACPI_FanPWMDutyCycle_Address, (byte)FanControlMode.Automatic);
        ECRAMWrite(ACPI_FanMode_Address, controlValue);
    }

    public override void SetFanDuty(double percent)
    {
        if (!IsOpen)
            return;

        // Convert 0-100 percentage to range
        byte fanSpeedSetpoint = (byte)(percent * (ECDetails.FanValueMax - ECDetails.FanValueMin) / 100 + ECDetails.FanValueMin);

        // Ensure the value is within the valid range
        fanSpeedSetpoint = Math.Min((byte)ECDetails.FanValueMax, Math.Max((byte)ECDetails.FanValueMin, fanSpeedSetpoint));

        // Set the requested fan speed
        ECRAMWrite(ACPI_FanPWMDutyCycle_Address, fanSpeedSetpoint);
    }
}