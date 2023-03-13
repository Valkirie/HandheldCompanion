using ControllerCommon;
using ControllerCommon.Actions;
using ControllerCommon.Devices;
using ControllerCommon.Inputs;
using GregsStack.InputSimulatorStandard.Native;
using HandheldCompanion.Actions;
using Newtonsoft.Json;
using System;
using System.Windows.Controls;

namespace HandheldCompanion.Controls
{
    /// <summary>
    /// Logique d'interaction pour LayoutTemplate.xaml
    /// </summary>
    /// 
    [JsonObject(MemberSerialization.OptIn)]
    public partial class LayoutTemplate : UserControl
    {
        [JsonProperty]
        public string Author
        {
            get
            {
                return _Author.Text;
            }

            set
            {
                _Author.Text = value;
            }
        }

        [JsonProperty]
        public string Name
        {
            get
            {
                return _Name.Text;
            }

            set
            {
                _Name.Text = value;
            }
        }

        [JsonProperty]
        public string Description
        {
            get
            {
                return _Description.Text;
            }

            set
            {
                _Description.Text = value;
            }
        }

        [JsonProperty]
        public string Executable { get; set; } = string.Empty;

        [JsonProperty]
        public bool IsTemplate { get; set; } = false;
        [JsonProperty]
        public bool IsCommunity { get; set; } = false;

        [JsonProperty]
        public Layout Layout { get; set; } = new();

        [JsonProperty]
        public Type DeviceType { get; set; }

        #region events
        public event UpdatedEventHandler Updated;
        public delegate void UpdatedEventHandler(LayoutTemplate layoutTemplate);
        #endregion

        public LayoutTemplate()
        {
            InitializeComponent();

            this.Layout.Updated += Layout_Updated;
        }

        public LayoutTemplate(string name, string description, string author, bool isTemplate, bool isCommunity, Type deviceType = null) : this()
        {
            this.Name = name;
            this.Description = description;
            this.Author = author;

            this.IsTemplate = isTemplate;
            this.IsCommunity = isCommunity;

            this.DeviceType = deviceType;

            this.Layout = new("Default");

            switch (this.Name)
            {
                case "Desktop":
                    {
                        this.Layout.AxisLayout = new()
                        {
                            { AxisLayoutFlags.LeftThumb, new MouseActions() { MouseType = MouseActionsType.Scroll } },
                            { AxisLayoutFlags.RightThumb, new MouseActions() { MouseType = MouseActionsType.Move, Sensivity = 20.0f } },
                            { AxisLayoutFlags.LeftPad, new MouseActions() { MouseType = MouseActionsType.Scroll } },
                            { AxisLayoutFlags.RightPad, new MouseActions() { MouseType = MouseActionsType.Move, Sensivity = 10.0f } },
                        };

                        this.Layout.ButtonLayout = new()
                        {
                            { ButtonFlags.B1, new KeyboardActions() { Key = VirtualKeyCode.RETURN } },
                            { ButtonFlags.B2, new KeyboardActions() { Key = VirtualKeyCode.SPACE } },
                            { ButtonFlags.B3, new KeyboardActions() { Key = VirtualKeyCode.PRIOR } },
                            { ButtonFlags.B4, new KeyboardActions() { Key = VirtualKeyCode.NEXT } },

                            { ButtonFlags.L1, new KeyboardActions() { Key = VirtualKeyCode.LCONTROL } },
                            { ButtonFlags.R1, new KeyboardActions() { Key = VirtualKeyCode.LMENU } },

                            { ButtonFlags.Back, new KeyboardActions() { Key = VirtualKeyCode.TAB } },
                            { ButtonFlags.Start, new KeyboardActions() { Key = VirtualKeyCode.ESCAPE } },

                            { ButtonFlags.DPadUp, new KeyboardActions() { Key = VirtualKeyCode.UP } },
                            { ButtonFlags.DPadDown, new KeyboardActions() { Key = VirtualKeyCode.DOWN } },
                            { ButtonFlags.DPadLeft, new KeyboardActions() { Key = VirtualKeyCode.LEFT } },
                            { ButtonFlags.DPadRight, new KeyboardActions() { Key = VirtualKeyCode.RIGHT } },

                            { ButtonFlags.L2, new MouseActions() { MouseType = MouseActionsType.RightButton } },
                            { ButtonFlags.R2, new MouseActions() { MouseType = MouseActionsType.LeftButton } },

                            { ButtonFlags.LeftThumb, new KeyboardActions() { Key = VirtualKeyCode.LWIN } },
                            { ButtonFlags.RightThumb, new KeyboardActions() { Key = VirtualKeyCode.LSHIFT } },

                            { ButtonFlags.LeftPadClick, new MouseActions() { MouseType = MouseActionsType.RightButton } },
                            { ButtonFlags.RightPadClick, new MouseActions() { MouseType = MouseActionsType.LeftButton } },
                        };
                    }
                    break;

                case "Gamepad (Nintendo)":
                    {
                        this.Layout.ButtonLayout[ButtonFlags.B1] = new ButtonActions() { Button = ButtonFlags.B2 };
                        this.Layout.ButtonLayout[ButtonFlags.B2] = new ButtonActions() { Button = ButtonFlags.B1 };
                        this.Layout.ButtonLayout[ButtonFlags.B3] = new ButtonActions() { Button = ButtonFlags.B4 };
                        this.Layout.ButtonLayout[ButtonFlags.B4] = new ButtonActions() { Button = ButtonFlags.B3 };
                    }
                    break;

                case "Keyboard (WASD) and Mouse":
                    {
                        this.Layout.AxisLayout = new()
                        {
                            { AxisLayoutFlags.LeftThumb, new EmptyActions() },
                            { AxisLayoutFlags.RightThumb, new MouseActions() { MouseType = MouseActionsType.Move, Sensivity = 20.0f } },
                            { AxisLayoutFlags.LeftPad, new EmptyActions() },
                            { AxisLayoutFlags.RightPad, new MouseActions() { MouseType = MouseActionsType.Move, Sensivity = 10.0f } },
                        };

                        this.Layout.ButtonLayout = new()
                        {
                            { ButtonFlags.B1, new KeyboardActions() { Key = VirtualKeyCode.SPACE } },
                            { ButtonFlags.B2, new KeyboardActions() { Key = VirtualKeyCode.VK_E } },
                            { ButtonFlags.B3, new KeyboardActions() { Key = VirtualKeyCode.VK_R } },
                            { ButtonFlags.B4, new KeyboardActions() { Key = VirtualKeyCode.VK_F } },

                            { ButtonFlags.L1, new MouseActions() { MouseType = MouseActionsType.ScrollDown } },
                            { ButtonFlags.R1, new MouseActions() { MouseType = MouseActionsType.ScrollUp } },

                            { ButtonFlags.Back, new KeyboardActions() { Key = VirtualKeyCode.TAB } },
                            { ButtonFlags.Start, new KeyboardActions() { Key = VirtualKeyCode.ESCAPE } },

                            { ButtonFlags.DPadUp, new KeyboardActions() { Key = VirtualKeyCode.UP } },
                            { ButtonFlags.DPadDown, new KeyboardActions() { Key = VirtualKeyCode.DOWN } },
                            { ButtonFlags.DPadLeft, new KeyboardActions() { Key = VirtualKeyCode.LEFT } },
                            { ButtonFlags.DPadRight, new KeyboardActions() { Key = VirtualKeyCode.RIGHT } },

                            { ButtonFlags.L2, new MouseActions() { MouseType = MouseActionsType.LeftButton } },
                            { ButtonFlags.R2, new MouseActions() { MouseType = MouseActionsType.RightButton } },

                            { ButtonFlags.LeftThumbUp, new KeyboardActions() { Key = VirtualKeyCode.VK_W } },
                            { ButtonFlags.LeftThumbDown, new KeyboardActions() { Key = VirtualKeyCode.VK_S } },
                            { ButtonFlags.LeftThumbLeft, new KeyboardActions() { Key = VirtualKeyCode.VK_A } },
                            { ButtonFlags.LeftThumbRight, new KeyboardActions() { Key = VirtualKeyCode.VK_D } },
                            { ButtonFlags.LeftThumb, new KeyboardActions() { Key = VirtualKeyCode.LSHIFT } },
                            { ButtonFlags.RightThumb, new MouseActions() { MouseType = MouseActionsType.LeftButton } },

                            { ButtonFlags.LeftPadClickUp, new KeyboardActions() { Key = VirtualKeyCode.VK_1 } },
                            { ButtonFlags.LeftPadClickDown, new KeyboardActions() { Key = VirtualKeyCode.VK_3 } },
                            { ButtonFlags.LeftPadClickLeft, new KeyboardActions() { Key = VirtualKeyCode.VK_4 } },
                            { ButtonFlags.LeftPadClickRight, new KeyboardActions() { Key = VirtualKeyCode.VK_2 } },
                            { ButtonFlags.RightPadClick, new MouseActions() { MouseType = MouseActionsType.LeftButton } },
                        };
                    }
                    break;

                case "Gamepad with Mouse Trackpad":
                    {
                        this.Layout.AxisLayout[AxisLayoutFlags.RightPad] = new MouseActions() { MouseType = MouseActionsType.Move };
                    }
                    break;

                case "Gamepad with Joystick Trackpad":
                    {
                        this.Layout.AxisLayout[AxisLayoutFlags.RightPad] = new AxisActions() { Axis = AxisLayoutFlags.RightThumb };
                    }
                    break;
            }
        }

        private void Layout_Updated(Layout layout)
        {
            Updated?.Invoke(this);
        }

        public static LayoutTemplate DesktopLayout = new LayoutTemplate("Desktop", "Layout for Desktop Browsing", "HandheldCompanion", true, false);
        public static LayoutTemplate DefaultLayout = new LayoutTemplate("Gamepad (XBOX)", "This template is for games that already have built-in gamepad support. Intended for dual stick games such as twin-stick shooters, side-scrollers, etc.", "HandheldCompanion", true, false);
        public static LayoutTemplate NintendoLayout = new LayoutTemplate("Gamepad (Nintendo)", "This template is for games that already have built-in gamepad support. Intended for games that are designed with a Nintendo gamepad in mind.", "HandheldCompanion", true, false);
        public static LayoutTemplate KeyboardLayout = new LayoutTemplate("Keyboard (WASD) and Mouse", "This template works great for the games that were designed with a keyboard and mouse in mind, without gamepad support. The controller will drive the game's keyboard based events with buttons, but will make assumptions about which buttons move you around (WASD for movement, space for jump, etc.). The right pad will emulate the movement of a mouse.", "HandheldCompanion", true, false);
        public static LayoutTemplate GamepadMouseLayout = new LayoutTemplate("Gamepad with Mouse Trackpad", "This template is for games that already have built-in gamepad support. The right trackpad will be bound to mouse emulation which may not work in all games.", "HandheldCompanion", true, false, typeof(SteamDeck));
        public static LayoutTemplate GamepadJoystickLayout = new LayoutTemplate("Gamepad with Joystick Trackpad", "This template is for games that already have built-in gamepad support and have a third person controlled camera. FPS or Third Person Adventure games, etc.", "HandheldCompanion", true, false, typeof(SteamDeck));
    }
}
