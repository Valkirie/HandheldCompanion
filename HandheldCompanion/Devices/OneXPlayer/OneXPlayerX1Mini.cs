namespace HandheldCompanion.Devices;

public class OneXPlayerX1Mini : OneXPlayerX1
{
    public OneXPlayerX1Mini()
    {
        // https://www.amd.com/fr/products/processors/laptop/ryzen/8000-series/amd-ryzen-7-8840u.html
        nTDP = new double[] { 15, 15, 28 };
        cTDP = new double[] { 15, 30 };
        GfxClock = new double[] { 100, 2700 };
        CpuClock = 5100;

        EnableSerialPort = false;
    }

    public override bool IsBatteryProtectionSupported(int majorVersion, int minorVersion)
    {
        return majorVersion >= 1 && minorVersion >= 3;
    }
}
