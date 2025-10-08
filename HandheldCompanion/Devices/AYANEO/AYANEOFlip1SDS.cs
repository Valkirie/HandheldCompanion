using HandheldCompanion.Inputs;
using HandheldCompanion.Managers;
using WindowsInput.Events;

namespace HandheldCompanion.Devices.AYANEO
{
    public class AYANEOFlip1SDS : AYANEOFlipDS
    {
        public AYANEOFlip1SDS()
        {
            // https://www.amd.com/en/products/processors/laptop/ryzen/300-series/amd-ryzen-ai-9-365.html
            GfxClock = new double[] { 100, 2900 };
            CpuClock = 5100;

            this.OEMChords.Add(new KeyboardChord("Custom Key Big", [KeyCode.F23], [KeyCode.F23], false, ButtonFlags.OEM1));
            this.OEMChords.Add(new KeyboardChord("Custom Key Small", [KeyCode.F24], [KeyCode.F24], false, ButtonFlags.OEM2));
            this.OEMChords.Add(new KeyboardChord("Custom Key Top Left", [KeyCode.F21], [KeyCode.F21], false, ButtonFlags.OEM3));
            this.OEMChords.Add(new KeyboardChord("Custom Key Top Right", [KeyCode.F22], [KeyCode.F22], false, ButtonFlags.OEM4));
            this.OEMChords.Add(new KeyboardChord("Custom Key Screen", [KeyCode.F18], [KeyCode.F18], false, ButtonFlags.OEM5));
        }

        public override void Initialize(bool FirstStart, bool NewUpdate)
        {
            base.Initialize(FirstStart, NewUpdate);

            if (FirstStart)
            {
                ManagerFactory.settingsManager.SetProperty("QuickToolsDeviceName", "AYALCD_QHD");
            }
        }
    }
}