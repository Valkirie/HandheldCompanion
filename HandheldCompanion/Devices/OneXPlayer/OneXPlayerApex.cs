using System.Collections.Generic;
using System.Numerics;
using HandheldCompanion.Commands.Functions.HC;
using HandheldCompanion.Commands.Functions.Windows;
using HandheldCompanion.Inputs;
using WindowsInput.Events;

namespace HandheldCompanion.Devices.OneXPlayer
{
    public class OneXPlayerApex : OneXPlayerX1AMD
    {
        public OneXPlayerApex()
        {
            ProductIllustration = "device_onexplayer_apex";
            ProductModel = "ONEXPLAYERAPEX";
            VendorHidInitProfile = OxpHidInitProfile.Apex;

            GyroMatrix = new()
            {
                Axis = new Vector3(1.0f, -1.0f, 1.0f),
                AxisSwap = new SortedDictionary<char, char>
                {
                    { 'X', 'Y' },
                    { 'Y', 'X' },
                    { 'Z', 'Z' },
                }
            };

            AcceleroMatrix = new()
            {
                Axis = new Vector3(-1.0f, 1.0f, -1.0f),
                AxisSwap = new SortedDictionary<char, char>
                {
                    { 'X', 'Y' },
                    { 'Y', 'X' },
                    { 'Z', 'Z' },
                }
            };

            nTDP = new double[] { 25, 35, 65 };
            cTDP = new double[] { 25, 65 };
            GfxClock = new double[] { 100, 2900 };
            CpuClock = 5100;

            OEMChords.Clear();

            OEMChords.Add(new KeyboardChord("Turbo",
                [KeyCode.LControl, KeyCode.LMenu, KeyCode.LWin],
                [KeyCode.LWin, KeyCode.LMenu, KeyCode.LControl],
                false, ButtonFlags.OEM1
                ));

            OEMChords.Add(new KeyboardChord("Keyboard",
                [KeyCode.LControl, KeyCode.LWin, KeyCode.O],
                [KeyCode.O, KeyCode.LWin, KeyCode.LControl],
                false, ButtonFlags.OEM2
                ));

            OEMChords.Add(new KeyboardChord("Orange",
                [KeyCode.LWin, KeyCode.G],
                [KeyCode.G, KeyCode.LWin],
                false, ButtonFlags.OEM3
                ));

            OEMChords.Add(new KeyboardChord("Keyboard + Orange",
                [KeyCode.RControlKey, KeyCode.RAlt, KeyCode.Delete],
                [KeyCode.Delete, KeyCode.RAlt, KeyCode.RControlKey],
                false, ButtonFlags.OEM4
                ));

            OEMChords.Add(new KeyboardChord("Turbo + Orange",
                [KeyCode.LWin, KeyCode.Snapshot],
                [KeyCode.Snapshot, KeyCode.LWin],
                false, ButtonFlags.OEM5
                ));

            OEMChords.Add(new KeyboardChord("Orange, Long-press",
                [KeyCode.LWin, KeyCode.D],
                [KeyCode.D, KeyCode.LWin],
                false, ButtonFlags.OEM6
                ));

            DeviceHotkeys[typeof(MainWindowCommands)].inputsChord.ButtonState[ButtonFlags.OEM3] = true;
            DeviceHotkeys[typeof(MainWindowCommands)].InputsChordType = InputsChordType.Click;
            DeviceHotkeys[typeof(QuickToolsCommands)].inputsChord.ButtonState[ButtonFlags.OEM1] = true;
            DeviceHotkeys[typeof(OnScreenKeyboardCommands)].inputsChord.ButtonState[ButtonFlags.OEM2] = true;
        }

        protected override ButtonFlags MapVendorButton(byte buttonId)
        {
            return buttonId switch
            {
                0x21 => ButtonFlags.OEM3,
                0x22 => ButtonFlags.R4,
                0x23 => ButtonFlags.L4,
                0x24 => ButtonFlags.OEM2,
                _ => base.MapVendorButton(buttonId),
            };
        }

        public override string GetGlyph(ButtonFlags button)
        {
            switch (button)
            {
                case ButtonFlags.OEM1:
                    return "\u2211";
                case ButtonFlags.OEM2:
                    return "\u2210";
                case ButtonFlags.OEM3:
                    return "\u2219";
            }

            return base.GetGlyph(button);
        }
    }
}
