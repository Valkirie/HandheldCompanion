using HandheldCompanion.Inputs;
using WindowsInput.Events;

namespace HandheldCompanion.Devices;

public class AYANEOFlipDS : AYANEOFlipKB
{
    public AYANEOFlipDS()
    {
        // device specific settings
        this.ProductIllustration = "device_aya_flip_ds";
        this.ProductModel = "AYANEO Flip DS";

        // TODO: Check if there really is no RGB but looks like it
        this.Capabilities -= DeviceCapabilities.DynamicLighting;
        this.Capabilities -= DeviceCapabilities.DynamicLightingBrightness;

        // TODO: Add OEMChords for "Dual-Screen Keys" key here
        this.OEMChords.Add(new KeyboardChord("Custom Key Screen",
            [KeyCode.RControlKey, KeyCode.LWin, KeyCode.F18],
            [KeyCode.F18, KeyCode.LWin, KeyCode.RControlKey],
            false, ButtonFlags.OEM5
        ));
    }

    public override string GetGlyph(ButtonFlags button)
    {
        switch (button)
        {
            case ButtonFlags.OEM5:
                return "\u23CD";
        }

        return base.GetGlyph(button);
    }

    protected void CEcControl_SetSecDispBrightness(short brightness)
    {
        this.ECRAMWrite(0x4e, (byte)((brightness * 0xff) / 100));
    }
}