﻿using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using ControllerCommon;
using ControllerCommon.Actions;
using ControllerCommon.Inputs;
using GregsStack.InputSimulatorStandard.Native;
using HandheldCompanion.Actions;
using HandheldCompanion.Controllers;
using Newtonsoft.Json;

namespace HandheldCompanion.Controls;

/// <summary>
///     Logique d'interaction pour LayoutTemplate.xaml
/// </summary>
[JsonObject(MemberSerialization.OptIn)]
public partial class LayoutTemplate : UserControl, IComparable
{
    public static readonly LayoutTemplate DesktopLayout =
        new("Desktop", "Layout for Desktop Browsing", "HandheldCompanion", true);

    public static readonly LayoutTemplate DefaultLayout = new("Gamepad (XBOX)",
        "This template is for games that already have built-in gamepad support. Intended for dual stick games such as twin-stick shooters, side-scrollers, etc.",
        "HandheldCompanion", true);

    public static readonly LayoutTemplate NintendoLayout = new("Gamepad (Nintendo)",
        "This template is for games that already have built-in gamepad support. Intended for games that are designed with a Nintendo gamepad in mind.",
        "HandheldCompanion", true);

    public static readonly LayoutTemplate KeyboardLayout = new("Keyboard (WASD) and Mouse",
        "This template works great for the games that were designed with a keyboard and mouse in mind, without gamepad support. The controller will drive the game's keyboard based events with buttons, but will make assumptions about which buttons move you around (WASD for movement, space for jump, etc.). The right pad will emulate the movement of a mouse.",
        "HandheldCompanion", true);

    public static readonly LayoutTemplate GamepadMouseLayout = new("Gamepad with Mouse Trackpad",
        "This template is for games that already have built-in gamepad support. The right trackpad will be bound to mouse emulation which may not work in all games.",
        "HandheldCompanion", true, typeof(NeptuneController));

    public static readonly LayoutTemplate GamepadJoystickLayout = new("Gamepad with Joystick Trackpad",
        "This template is for games that already have built-in gamepad support and have a third person controlled camera. FPS or Third Person Adventure games, etc.",
        "HandheldCompanion", true, typeof(NeptuneController));

    public LayoutTemplate()
    {
        InitializeComponent();
    }

    public LayoutTemplate(Layout layout) : this()
    {
        Layout = layout;
        Layout.Updated += Layout_Updated;
    }

    private LayoutTemplate(string name, string description, string author, bool isInternal,
        Type deviceType = null) : this()
    {
        Name = name;
        Description = description;
        Author = author;
        Product = string.Empty;

        IsInternal = isInternal;
        ControllerType = deviceType;

        Layout = new Layout(true);

        switch (Name)
        {
            case "Desktop":
            {
                Layout.AxisLayout = new SortedDictionary<AxisLayoutFlags, IActions>
                {
                    {
                        AxisLayoutFlags.LeftThumb,
                        new MouseActions { MouseType = MouseActionsType.Scroll, Sensivity = 25.0f }
                    },
                    {
                        AxisLayoutFlags.RightThumb,
                        new MouseActions { MouseType = MouseActionsType.Move, Sensivity = 25.0f }
                    },
                    {
                        AxisLayoutFlags.LeftPad,
                        new MouseActions { MouseType = MouseActionsType.Scroll, Sensivity = 25.0f }
                    },
                    {
                        AxisLayoutFlags.RightPad,
                        new MouseActions { MouseType = MouseActionsType.Move, Sensivity = 25.0f }
                    }
                };

                Layout.ButtonLayout = new SortedDictionary<ButtonFlags, IActions>
                {
                    { ButtonFlags.B1, new KeyboardActions { Key = VirtualKeyCode.RETURN } },
                    { ButtonFlags.B2, new KeyboardActions { Key = VirtualKeyCode.ESCAPE } },
                    { ButtonFlags.B3, new KeyboardActions { Key = VirtualKeyCode.PRIOR } },
                    { ButtonFlags.B4, new KeyboardActions { Key = VirtualKeyCode.NEXT } },

                    { ButtonFlags.L1, new KeyboardActions { Key = VirtualKeyCode.BACK } },
                    { ButtonFlags.R1, new KeyboardActions { Key = VirtualKeyCode.SPACE } },

                    { ButtonFlags.Back, new KeyboardActions { Key = VirtualKeyCode.MENU } },
                    { ButtonFlags.Start, new KeyboardActions { Key = VirtualKeyCode.TAB } },

                    { ButtonFlags.DPadUp, new KeyboardActions { Key = VirtualKeyCode.UP } },
                    { ButtonFlags.DPadDown, new KeyboardActions { Key = VirtualKeyCode.DOWN } },
                    { ButtonFlags.DPadLeft, new KeyboardActions { Key = VirtualKeyCode.LEFT } },
                    { ButtonFlags.DPadRight, new KeyboardActions { Key = VirtualKeyCode.RIGHT } },

                    { ButtonFlags.L2, new MouseActions { MouseType = MouseActionsType.RightButton } },
                    { ButtonFlags.R2, new MouseActions { MouseType = MouseActionsType.LeftButton } },

                    { ButtonFlags.LeftPadClick, new MouseActions { MouseType = MouseActionsType.RightButton } },
                    { ButtonFlags.RightPadClick, new MouseActions { MouseType = MouseActionsType.LeftButton } }
                };
            }
                break;

            case "Gamepad (Nintendo)":
            {
                Layout.ButtonLayout[ButtonFlags.B1] = new ButtonActions { Button = ButtonFlags.B2 };
                Layout.ButtonLayout[ButtonFlags.B2] = new ButtonActions { Button = ButtonFlags.B1 };
                Layout.ButtonLayout[ButtonFlags.B3] = new ButtonActions { Button = ButtonFlags.B4 };
                Layout.ButtonLayout[ButtonFlags.B4] = new ButtonActions { Button = ButtonFlags.B3 };
            }
                break;

            case "Keyboard (WASD) and Mouse":
            {
                Layout.AxisLayout = new SortedDictionary<AxisLayoutFlags, IActions>
                {
                    { AxisLayoutFlags.LeftThumb, new EmptyActions() },
                    {
                        AxisLayoutFlags.RightThumb,
                        new MouseActions { MouseType = MouseActionsType.Move, Sensivity = 25.0f }
                    },
                    { AxisLayoutFlags.LeftPad, new EmptyActions() },
                    {
                        AxisLayoutFlags.RightPad,
                        new MouseActions { MouseType = MouseActionsType.Move, Sensivity = 25.0f }
                    }
                };

                Layout.ButtonLayout = new SortedDictionary<ButtonFlags, IActions>
                {
                    { ButtonFlags.B1, new KeyboardActions { Key = VirtualKeyCode.SPACE } },
                    { ButtonFlags.B2, new KeyboardActions { Key = VirtualKeyCode.VK_E } },
                    { ButtonFlags.B3, new KeyboardActions { Key = VirtualKeyCode.VK_R } },
                    { ButtonFlags.B4, new KeyboardActions { Key = VirtualKeyCode.VK_F } },

                    { ButtonFlags.L1, new MouseActions { MouseType = MouseActionsType.ScrollDown, Sensivity = 25.0f } },
                    { ButtonFlags.R1, new MouseActions { MouseType = MouseActionsType.ScrollUp, Sensivity = 25.0f } },

                    { ButtonFlags.Back, new KeyboardActions { Key = VirtualKeyCode.TAB } },
                    { ButtonFlags.Start, new KeyboardActions { Key = VirtualKeyCode.ESCAPE } },

                    { ButtonFlags.DPadUp, new KeyboardActions { Key = VirtualKeyCode.VK_1 } },
                    { ButtonFlags.DPadDown, new KeyboardActions { Key = VirtualKeyCode.VK_3 } },
                    { ButtonFlags.DPadLeft, new KeyboardActions { Key = VirtualKeyCode.VK_4 } },
                    { ButtonFlags.DPadRight, new KeyboardActions { Key = VirtualKeyCode.VK_2 } },

                    { ButtonFlags.L2, new MouseActions { MouseType = MouseActionsType.RightButton } },
                    { ButtonFlags.R2, new MouseActions { MouseType = MouseActionsType.LeftButton } },

                    { ButtonFlags.LeftThumbUp, new KeyboardActions { Key = VirtualKeyCode.VK_W } },
                    { ButtonFlags.LeftThumbDown, new KeyboardActions { Key = VirtualKeyCode.VK_S } },
                    { ButtonFlags.LeftThumbLeft, new KeyboardActions { Key = VirtualKeyCode.VK_A } },
                    { ButtonFlags.LeftThumbRight, new KeyboardActions { Key = VirtualKeyCode.VK_D } },

                    { ButtonFlags.LeftThumb, new KeyboardActions { Key = VirtualKeyCode.LSHIFT } },
                    { ButtonFlags.RightThumb, new MouseActions { MouseType = MouseActionsType.LeftButton } },

                    { ButtonFlags.LeftPadClickUp, new KeyboardActions { Key = VirtualKeyCode.VK_1 } },
                    { ButtonFlags.LeftPadClickDown, new KeyboardActions { Key = VirtualKeyCode.VK_3 } },
                    { ButtonFlags.LeftPadClickLeft, new KeyboardActions { Key = VirtualKeyCode.VK_4 } },
                    { ButtonFlags.LeftPadClickRight, new KeyboardActions { Key = VirtualKeyCode.VK_2 } },

                    { ButtonFlags.RightPadClick, new MouseActions { MouseType = MouseActionsType.LeftButton } }
                };
            }
                break;

            case "Gamepad with Mouse Trackpad":
            {
                Layout.AxisLayout[AxisLayoutFlags.RightPad] = new MouseActions { MouseType = MouseActionsType.Move };
            }
                break;

            case "Gamepad with Joystick Trackpad":
            {
                Layout.AxisLayout[AxisLayoutFlags.RightPad] = new AxisActions { Axis = AxisLayoutFlags.RightThumb };
            }
                break;
        }
    }

    [JsonProperty]
    public string Author
    {
        get => _Author.Text;
        set => _Author.Text = value;
    }

    [JsonProperty]
    public string Name
    {
        get => _Name.Text;
        set => _Name.Text = value;
    }

    [JsonProperty]
    public string Description
    {
        get => _Description.Text;
        set => _Description.Text = value;
    }

    [JsonProperty]
    public string Product
    {
        get => _Product.Text;
        set
        {
            _Product.Text = value;
            _Product.Visibility = string.IsNullOrEmpty(value) ? Visibility.Collapsed : Visibility.Visible;
        }
    }

    [JsonProperty] public Guid Guid { get; set; } = Guid.NewGuid();

    [JsonProperty] public string Executable { get; set; } = string.Empty;

    [JsonProperty] public bool IsInternal { get; set; }

    [JsonProperty] public Layout Layout { get; set; } = new();

    [JsonProperty] public Type ControllerType { get; set; }

    public int CompareTo(object obj)
    {
        var profile = (LayoutTemplate)obj;
        return profile.Name.CompareTo(Name);
    }

    private void Layout_Updated(Layout layout)
    {
        Updated?.Invoke(this);
    }

    #region events

    public event UpdatedEventHandler Updated;

    public delegate void UpdatedEventHandler(LayoutTemplate layoutTemplate);

    #endregion
}