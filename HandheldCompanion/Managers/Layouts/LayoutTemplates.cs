using ControllerCommon;
using ControllerCommon.Inputs;
using GregsStack.InputSimulatorStandard.Native;
using HandheldCompanion.Actions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static HandheldCompanion.Simulators.MouseSimulator;

namespace HandheldCompanion.Managers.Layouts
{
    public struct LayoutTemplates
    {
        public static Layout DesktopLayout = new Layout("Desktop")
        {
            AxisLayout = new()
            {
                { AxisLayoutFlags.LeftThumb, new MouseActions() { MouseType = MouseActionsType.Scroll } },
                { AxisLayoutFlags.RightThumb, new MouseActions() { MouseType = MouseActionsType.Move, Sensivity = 20.0f } },
                { AxisLayoutFlags.LeftPad, new MouseActions() { MouseType = MouseActionsType.Scroll } },
                { AxisLayoutFlags.RightPad, new MouseActions() { MouseType = MouseActionsType.Move, Sensivity = 10.0f } },
            },

            ButtonLayout = new()
            {
                { ButtonFlags.Start, new KeyboardActions() { Key = VirtualKeyCode.ESCAPE } },
                { ButtonFlags.Back, new KeyboardActions() { Key = VirtualKeyCode.TAB } },

                { ButtonFlags.L1, new KeyboardActions() { Key = VirtualKeyCode.LCONTROL } },
                { ButtonFlags.R1, new KeyboardActions() { Key = VirtualKeyCode.LMENU } },

                { ButtonFlags.L2, new MouseActions() { MouseType = MouseActionsType.RightButton } },
                { ButtonFlags.R2, new MouseActions() { MouseType = MouseActionsType.LeftButton } },

                { ButtonFlags.LeftPadClick, new MouseActions() { MouseType = MouseActionsType.RightButton } },
                { ButtonFlags.RightPadClick, new MouseActions() { MouseType = MouseActionsType.LeftButton } },

                { ButtonFlags.DPadUp, new KeyboardActions() { Key = VirtualKeyCode.UP } },
                { ButtonFlags.DPadDown, new KeyboardActions() { Key = VirtualKeyCode.DOWN } },
                { ButtonFlags.DPadLeft, new KeyboardActions() { Key = VirtualKeyCode.LEFT } },
                { ButtonFlags.DPadRight, new KeyboardActions() { Key = VirtualKeyCode.RIGHT } },

                { ButtonFlags.LeftThumb, new KeyboardActions() { Key = VirtualKeyCode.LWIN } },
                { ButtonFlags.RightThumb, new KeyboardActions() { Key = VirtualKeyCode.LSHIFT } },

                { ButtonFlags.B1, new KeyboardActions() { Key = VirtualKeyCode.RETURN } },
                { ButtonFlags.B2, new KeyboardActions() { Key = VirtualKeyCode.SPACE } },
                { ButtonFlags.B3, new KeyboardActions() { Key = VirtualKeyCode.PRIOR } },
                { ButtonFlags.B4, new KeyboardActions() { Key = VirtualKeyCode.NEXT } },
            }
        };
    }
}
