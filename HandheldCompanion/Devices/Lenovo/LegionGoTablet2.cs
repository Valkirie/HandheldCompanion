using System.Numerics;

namespace HandheldCompanion.Devices.Lenovo
{
    public class LegionGoTablet2 : LegionGoTablet
    {
        public LegionGoTablet2()
        {
            // https://www.amd.com/en/products/processors/handhelds/ryzen-z-series/z2-series/z2-extreme.html
            nTDP = new double[] { 15, 15, 20 };
            cTDP = new double[] { 15, 35 };
            GfxClock = new double[] { 100, 2900 };
            CpuClock = 5000;

            GyrometerAxis = new Vector3(1.0f, 1.0f, -1.0f);
            AccelerometerAxis = new Vector3(-1.0f, -1.0f, 1.0f);
        }
    }
}
