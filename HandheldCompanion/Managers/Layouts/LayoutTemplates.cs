﻿using ControllerCommon;
using ControllerCommon.Actions;
using ControllerCommon.Inputs;
using GregsStack.InputSimulatorStandard.Native;
using HandheldCompanion.Actions;
using LiveCharts.Wpf;
using System.Collections.Generic;

namespace HandheldCompanion.Managers.Layouts
{
    public class LayoutTemplate
    {
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Author { get; set; } = string.Empty;
        public string Executable { get; set; } = string.Empty;

        public bool IsTemplate { get; set; } = false;
        public bool IsCommunity { get; set; } = false;

        public Layout Layout = new();

        #region events
        public event UpdatedEventHandler Updated;
        public delegate void UpdatedEventHandler(LayoutTemplate layoutTemplate);
        #endregion

        public LayoutTemplate()
        {
            this.Layout.Updated += Layout_Updated;
        }

        public LayoutTemplate(string name, string description, string author, bool isTemplate, bool isCommunity) : this()
        {
            this.Name = name;
            this.Description = description;
            this.Author = author;

            this.IsTemplate = isTemplate;
            this.IsCommunity = isCommunity;
        }

        public LayoutTemplate(string name, string description, string author, bool isTemplate, bool isCommunity, Dictionary<AxisLayoutFlags, IActions> axisLayout, Dictionary<ButtonFlags, IActions> buttonLayout) : this(name, description, author, isTemplate, isCommunity)
        {
            this.Layout.AxisLayout = axisLayout;
            this.Layout.ButtonLayout = buttonLayout;
        }

        private void Layout_Updated(Layout layout)
        {
            Updated?.Invoke(this);
        }

        public static LayoutTemplate DesktopLayout = new LayoutTemplate("Desktop", "Layout for Desktop Browsing", "HandheldCompanion", true, false, 
            new()
            {
                { AxisLayoutFlags.LeftThumb, new MouseActions() { MouseType = MouseActionsType.Scroll } },
                { AxisLayoutFlags.RightThumb, new MouseActions() { MouseType = MouseActionsType.Move, Sensivity = 20.0f } },
                { AxisLayoutFlags.LeftPad, new MouseActions() { MouseType = MouseActionsType.Scroll } },
                { AxisLayoutFlags.RightPad, new MouseActions() { MouseType = MouseActionsType.Move, Sensivity = 10.0f } },
            },
            new()
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
            });

        public static LayoutTemplate DefaultLayout = new LayoutTemplate("Gamepad", "The template works best for games that are designed with a gamepad in mind.", "HandheldCompanion", true, false)
        {
            Layout = new("Default")
        };

        public static LayoutTemplate NintendoLayout = new LayoutTemplate("Nintendo Gamepad", "The template works best for games that are designed with a Nintendo gamepad in mind.", "HandheldCompanion", true, false)
        {
            Layout = new("Nintendo")
        };
    }
}