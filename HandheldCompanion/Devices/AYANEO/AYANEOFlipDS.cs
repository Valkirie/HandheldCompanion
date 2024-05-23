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
    }

    protected void CEcControl_SetSecDispBrightness(short brightness)
    {
        this.ECRAMWrite(0x4e, (byte)((brightness * 0xff) / 100));
    }
}