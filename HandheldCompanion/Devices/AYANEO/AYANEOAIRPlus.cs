<<<<<<< HEAD
using HandheldCompanion.Devices.AYANEO;
using HandheldCompanion.Inputs;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Windows.Media;
using WindowsInput.Events;
using static HandheldCompanion.Utils.DeviceUtils;

namespace HandheldCompanion.Devices;

public class AYANEOAIRPlus : AYANEODevice
=======
using HandheldCompanion.Inputs;
using System.Collections.Generic;
using System.Numerics;

using WindowsInput.Events;

namespace HandheldCompanion.Devices;

public class AYANEOAIRPlus : IDevice
>>>>>>> f8fea3c25fb5fd254f5020d43305b7356ec9770d
{
    public AYANEOAIRPlus()
    {
        // device specific settings
        ProductIllustration = "device_aya_air";
        ProductModel = "AYANEOAir";

<<<<<<< HEAD
        GyrometerAxis = new Vector3(1.0f, -1.0f, -1.0f);
        GyrometerAxisSwap = new SortedDictionary<char, char>
=======
        AngularVelocityAxis = new Vector3(1.0f, -1.0f, -1.0f);
        AngularVelocityAxisSwap = new SortedDictionary<char, char>
>>>>>>> f8fea3c25fb5fd254f5020d43305b7356ec9770d
        {
            { 'X', 'X' },
            { 'Y', 'Z' },
            { 'Z', 'Y' }
        };

<<<<<<< HEAD
        AccelerometerAxis = new Vector3(-1.0f, -1.0f, -1.0f);
        AccelerometerAxisSwap = new SortedDictionary<char, char>
=======
        AccelerationAxis = new Vector3(-1.0f, -1.0f, -1.0f);
        AccelerationAxisSwap = new SortedDictionary<char, char>
>>>>>>> f8fea3c25fb5fd254f5020d43305b7356ec9770d
        {
            { 'X', 'X' },
            { 'Y', 'Z' },
            { 'Z', 'Y' }
        };

<<<<<<< HEAD
        // device specific capacities
        // todo, missing fan control
        Capabilities |= DeviceCapabilities.DynamicLighting;
        DynamicLightingCapabilities |= LEDLevel.SolidColor;
        DynamicLightingCapabilities |= LEDLevel.Ambilight;

        // Ayaneo Air Plus info based on:
        // https://github.com/JustEnoughLinuxOS/distribution/blob/main/packages/hardware/quirks/devices/AYANEO%20AIR%20Plus/bin/ledcontrol
        ECDetails = new ECDetails
        {
            AddressStatusCommandPort = 0x4E,
            AddressDataPort = 0x4F,
            AddressFanControl = 0x0,  // Unknown
            AddressFanDuty = 0x0,     // Unknown
            FanValueMin = 0,            // Unknown
            FanValueMax = 100           // Unknown
        };

=======
>>>>>>> f8fea3c25fb5fd254f5020d43305b7356ec9770d
        OEMChords.Add(new DeviceChord("Custom Key Top Right",
            new List<KeyCode> { KeyCode.LControl, KeyCode.LWin, KeyCode.F16 },
            new List<KeyCode> { KeyCode.F16, KeyCode.LControl, KeyCode.LWin },
            false, ButtonFlags.OEM3
        ));

        OEMChords.Add(new DeviceChord("Custom Key Top Left",
            new List<KeyCode> { KeyCode.LControl, KeyCode.LWin, KeyCode.F15 },
            new List<KeyCode> { KeyCode.F15, KeyCode.LControl, KeyCode.LWin },
            false, ButtonFlags.OEM4
        ));

        OEMChords.Add(new DeviceChord("Custom Key Big",
            new List<KeyCode> { KeyCode.LControl, KeyCode.LWin, KeyCode.F17 },
            new List<KeyCode> { KeyCode.F17, KeyCode.LControl, KeyCode.LWin },
            false, ButtonFlags.OEM1
        ));

        OEMChords.Add(new DeviceChord("Custom Key Small",
            new List<KeyCode> { KeyCode.LWin, KeyCode.D },
            new List<KeyCode> { KeyCode.D, KeyCode.LWin },
            false, ButtonFlags.OEM2
        ));
<<<<<<< HEAD
    }

    public override string GetGlyph(ButtonFlags button)
    {
        switch (button)
        {
            case ButtonFlags.OEM1:
                return "\uE003";
            case ButtonFlags.OEM2:
                return "\u220B";
            case ButtonFlags.OEM3:
                return "\u220A";
            case ButtonFlags.OEM4:
                return "\u2209";
        }

        return defaultGlyph;
    }

    private void LedOpen()
    {
        ECRamDirectWrite(0x87, ECDetails, 0xA5);
    }

    private void LedClose(byte param)
    {
        ECRamDirectWrite(param, ECDetails, 0x01);
    }

    private void LedAck()
    {
        ECRamDirectWrite(0x70, ECDetails, 0x00);
        LedClose(0x86);
    }

    private void LedEnable()
    {
        // Set LED ON
        LedState(0x37);

        LedOpen();
        ECRamDirectWrite(0x70, ECDetails, 0x00);
        LedClose(0x86);

        LedOpen();
        ECRamDirectWrite(0xB2, ECDetails, 0xBA);
        LedClose(0xC6);

        LedOpen();
        ECRamDirectWrite(0x72, ECDetails, 0xBA);
        LedClose(0x86);

        LedAck();
    }

    private void LedDisable()
    {
        // Set LED OFF
        LedState(0x31);
    }

    private void LedApply()
    {
        LedOpen();
        ECRamDirectWrite(0xBF, ECDetails, 0x00);
        LedClose(0xC6);

        LedOpen();
        ECRamDirectWrite(0x7F, ECDetails, 0x00);
        LedClose(0xC6);

        LedOpen();
        ECRamDirectWrite(0xC0, ECDetails, 0x00);
        LedClose(0xC6);

        LedOpen();
        ECRamDirectWrite(0x80, ECDetails, 0x00);
        LedClose(0x86);

        LedOpen();
        ECRamDirectWrite(0xC1, ECDetails, 0x05);
        LedClose(0xC6);

        LedOpen();
        ECRamDirectWrite(0x81, ECDetails, 0x05);
        LedClose(0xC6);

        LedOpen();
        ECRamDirectWrite(0xC2, ECDetails, 0x05);
        LedClose(0xC6);

        LedOpen();
        ECRamDirectWrite(0x82, ECDetails, 0x05);
        LedClose(0x86);

        LedOpen();
        ECRamDirectWrite(0xC3, ECDetails, 0x05);
        LedClose(0x86);

        LedOpen();
        ECRamDirectWrite(0x83, ECDetails, 0x05);
        LedClose(0x86);

        LedOpen();
        ECRamDirectWrite(0xC4, ECDetails, 0x05);
        LedClose(0xC6);

        LedOpen();
        ECRamDirectWrite(0x84, ECDetails, 0x05);
        LedClose(0x86);

        LedOpen();
        ECRamDirectWrite(0xC5, ECDetails, 0x07);
        LedClose(0xC6);

        LedOpen();
        ECRamDirectWrite(0x85, ECDetails, 0x07);
        LedClose(0x86);

        LedAck();
    }

    private void LedState(byte value)
    {
        // 0x31 = off
        // 0x37 = on
        byte[] zones = { 0xB2, 0x72 };

        LedOpen();
        foreach (byte zone in zones)
        {
            ECRamDirectWrite(zone, ECDetails, value);
            ECRamDirectWrite(0xC6, ECDetails, 0x01);
        }
        LedAck();
    }

    private void SetLEDColorL(Color color)
    {
        byte red = color.R;
        byte green = color.G;
        byte blue = color.B;

        LedOpen();
        for (byte i = 0xB3; i <= 0xBC; i += 3)
        {
            ECRamDirectWrite(i, ECDetails, red);
            ECRamDirectWrite((byte)(i + 1), ECDetails, green);
            ECRamDirectWrite((byte)(i + 2), ECDetails, blue);
        }
        LedAck();
    }

    private void SetLEDColorR(Color color)
    {
        byte red = color.R;
        byte green = color.G;
        byte blue = color.B;

        LedOpen();
        for (byte i = 0x73; i <= 0x7C; i += 3)
        {
            ECRamDirectWrite(i, ECDetails, red);
            ECRamDirectWrite((byte)(i + 1), ECDetails, green);
            ECRamDirectWrite((byte)(i + 2), ECDetails, blue);
        }
        LedAck();
    }

    private void SetLEDColor(Color color)
    {
        SetLEDColorL(color);
        SetLEDColorR(color);
    }

    public override bool ECRamDirectWrite(ushort address, ECDetails details, byte data)
    {
        ushort address2 = BitConverter.ToUInt16(new byte[] { (byte)address, 0xD1 }, 0);
        return base.ECRamDirectWrite(address2, details, data);
    }

    public override bool SetLedStatus(bool status)
    {
        switch(status)
        {
            case true:
                LedEnable();
                break;
            case false:
                LedDisable();
                break;
        }

        return true;
    }

    public override bool SetLedColor(Color MainColor, Color SecondaryColor, LEDLevel level, int speed)
    {
        if (!DynamicLightingCapabilities.HasFlag(level))
            return false;

        switch (level)
        {
            case LEDLevel.SolidColor:
                SetLEDColor(MainColor);
                break;
            case LEDLevel.Ambilight:
                SetLEDColorL(MainColor);
                SetLEDColorR(SecondaryColor);
                break;
        }

        LedApply();

        return true;
    }

    public override bool SetLedBrightness(int brightness)
    {
        // we might want to store colors on SetLedColor() and brightness on SetLedBrightness()
        // so that we can let people mess with brightness slider
        return base.SetLedBrightness(brightness);
    }

    /*
    private bool prevWasBlack = true;
    public override bool SetLedColor(Color MainColor, Color SecondaryColor, LEDLevel level)
    {
        if (MainColor == Colors.Black)
        {
            ayaneoLED.LedDisable();
            prevWasBlack = true;
            return true;
        }
        else if (prevWasBlack)
        {
            ayaneoLED.LedEnable();
            prevWasBlack = false;
        }

        // Set LED color
        ayaneoLED.SetLEDColor(MainColor);
        ayaneoLED.LedApply();

        return true;
    }

    public bool SetAmbilight(Color LEDColor1, Color LEDColor2)
    {
        // Set LED color
        ayaneoLED.SetLEDColorL(LEDColor1);
        ayaneoLED.SetLEDColorR(LEDColor2);
        ayaneoLED.LedApply();
        return true;
    }
    */
=======
    }
>>>>>>> f8fea3c25fb5fd254f5020d43305b7356ec9770d
}