using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using HandheldCompanion.Commands.Functions.HC;
using HandheldCompanion.Commands.Functions.Windows;
using HandheldCompanion.Inputs;
using HandheldCompanion.Managers;
using HandheldCompanion.Shared;
using WindowsInput.Events;

namespace HandheldCompanion.Devices.OneXPlayer
{
    public class OneXPlayerApex : OneXPlayerX1AMD
    {
        public OneXPlayerApex()
        {
            ProductIllustration = "device_onexplayer_apex";
            ProductModel = "ONEXPLAYERAPEX";
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

        protected override async Task ConfigureController()
        {
            WriteVendorHidCommand(0xB4, BuildRemapPage1(0x01));
            await Task.Delay(50);

            WriteVendorHidCommand(0xB4, BuildRemapPage2(0x01, 0x67, 0x66));
            await Task.Delay(50);

            WriteVendorHidCommand(0xB2, BuildRemapPage3());
            await Task.Delay(50);

            WriteVendorHidCommand(0xB2, BuildIntercept(false));
        }

        protected override byte[] BuildRemapPage1(byte preset) =>
        [
            0x02, 0x38, 0x02, 0x01, preset,
            0x01, 0x01, 0x01, 0x00, 0x00, 0x00,
            0x02, 0x01, 0x02, 0x00, 0x00, 0x00,
            0x03, 0x01, 0x03, 0x00, 0x00, 0x00,
            0x04, 0x01, 0x04, 0x00, 0x00, 0x00,
            0x05, 0x01, 0x05, 0x00, 0x00, 0x00,
            0x06, 0x01, 0x06, 0x00, 0x00, 0x00,
            0x07, 0x01, 0x07, 0x00, 0x00, 0x00,
            0x08, 0x01, 0x08, 0x00, 0x00, 0x00,
            0x09, 0x01, 0x09, 0x00, 0x00, 0x00,
        ];

        protected override byte[] BuildRemapPage2(byte preset, byte m1KeyCode, byte m2KeyCode) =>
        [
            0x02, 0x38, 0x02, 0x02, preset,
            0x0A, 0x01, 0x0A, 0x00, 0x00, 0x00,
            0x0B, 0x01, 0x0B, 0x00, 0x00, 0x00,
            0x0C, 0x01, 0x0C, 0x00, 0x00, 0x00,
            0x0D, 0x01, 0x0D, 0x00, 0x00, 0x00,
            0x0E, 0x01, 0x0E, 0x00, 0x00, 0x00,
            0x0F, 0x01, 0x0F, 0x00, 0x00, 0x00,
            0x10, 0x01, 0x10, 0x00, 0x00, 0x00,
            0x22, 0x02, 0x01, m1KeyCode, 0x00, 0x00,
            0x23, 0x02, 0x01, m2KeyCode, 0x00, 0x00,
        ];

        protected byte[] BuildRemapPage3(byte preset = 0x01) =>
        [
            0x01, 0x1F, 0x40, 0x03, 0x02, 0x03, 0x00, 0x00, 0x00, 0x01,
        ];

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
