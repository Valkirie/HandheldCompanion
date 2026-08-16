using HandheldCompanion.Inputs;
using HandheldCompanion.Devices.OneXPlayer;

namespace HandheldCompanion.Devices;

public class OneXPlayerX2MiniPro : OneXPlayerApex
{
    public OneXPlayerX2MiniPro()
    {
        // todo: ProductIllustration
        // ProductIllustration = "device_onexplayer_apex";
        ProductModel = "ONEXPLAYER X2Mini PRO";

        nTDP = new double[] { 15, 35, 54 };
        cTDP = new double[] { 15, 54 };
    }

    protected override ButtonFlags MapVendorButton(byte buttonId)
    {
        return buttonId switch
        {
            0x20 => ButtonFlags.OEM1,
            0x21 => ButtonFlags.OEM3,
            0x22 => ButtonFlags.L4,
            0x23 => ButtonFlags.R4,
            0x24 => ButtonFlags.OEM2,
            _ => base.MapVendorButton(buttonId),
        };
    }
}
