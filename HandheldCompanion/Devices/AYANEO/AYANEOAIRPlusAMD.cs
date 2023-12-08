namespace HandheldCompanion.Devices;

public class AYANEOAIRPlusAMD : AYANEOAIRPlus
{
    public AYANEOAIRPlusAMD()
    {
        // https://www.amd.com/en/products/apu/amd-ryzen-7-6800u
        nTDP = new double[] { 15, 15, 20 };
        cTDP = new double[] { 3, 33 };
        GfxClock = new double[] { 100, 2200 };
<<<<<<< HEAD
        CpuClock = 4700;
=======
>>>>>>> f8fea3c25fb5fd254f5020d43305b7356ec9770d
    }
}