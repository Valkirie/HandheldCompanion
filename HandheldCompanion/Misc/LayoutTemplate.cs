using GregsStack.InputSimulatorStandard.Native;

using HandheldCompanion.Actions;
using HandheldCompanion.Inputs;
using HandheldCompanion.Localization;

using Newtonsoft.Json;

using SharpDX.XInput;

using System;
using System.Collections.Generic;

namespace HandheldCompanion.Misc
{
    [JsonObject(MemberSerialization.OptIn)]
    public partial class LayoutTemplate : IComparable
    {
        public static readonly LayoutTemplate DesktopLayout = new("Desktop", "HandheldCompanion", true);

        public static readonly LayoutTemplate DefaultLayout = new("Default", "HandheldCompanion", true);

        public static readonly LayoutTemplate NintendoLayout = new("Nintendo", "HandheldCompanion", true);

        public static readonly LayoutTemplate KeyboardLayout = new("Keyboard", "HandheldCompanion", true);

        public static readonly LayoutTemplate SteamControllerLayout = CreateSteamControllerLayout();

        public static readonly LayoutTemplate GamepadMouseLayout = new("GamepadMouse", "HandheldCompanion", true, "NeptuneController");

        public static readonly LayoutTemplate GamepadJoystickLayout = new("GamepadJoystick", "HandheldCompanion", true, "NeptuneController");

        public LayoutTemplate()
        {
        }

        public LayoutTemplate(Layout layout) : this()
        {
            Layout = layout;
            Layout.Updated += Layout_Updated;
        }

        private static LayoutTemplate CreateSteamControllerLayout()
        {
            LayoutTemplate template = new("Steam Controller", "Layout for Steam Controller-style virtual devices.", "HandheldCompanion", true);

            template.Layout.ButtonLayout[ButtonFlags.DPadUp] =
            [
                CreateAxisAction(AxisLayoutFlags.LeftPad, y: 28672),
                CreateButtonAction(ButtonFlags.LeftPadTouch),
                CreateButtonAction(ButtonFlags.LeftPadClick)
            ];

            template.Layout.ButtonLayout[ButtonFlags.DPadDown] =
            [
                CreateButtonAction(ButtonFlags.LeftPadClick),
                CreateAxisAction(AxisLayoutFlags.LeftPad, y: -28672),
                CreateButtonAction(ButtonFlags.LeftPadTouch)
            ];

            template.Layout.ButtonLayout[ButtonFlags.DPadLeft] =
            [
                CreateButtonAction(ButtonFlags.LeftPadClick),
                CreateButtonAction(ButtonFlags.LeftPadTouch),
                CreateAxisAction(AxisLayoutFlags.LeftPad, x: -28672)
            ];

            template.Layout.ButtonLayout[ButtonFlags.DPadRight] =
            [
                CreateButtonAction(ButtonFlags.LeftPadTouch),
                CreateButtonAction(ButtonFlags.LeftPadClick),
                CreateAxisAction(AxisLayoutFlags.LeftPad, x: 28672)
            ];

            template.Layout.ButtonLayout[ButtonFlags.RightStickClick] = [CreateButtonAction(ButtonFlags.RightPadClick)];

            template.Layout.AxisLayout[AxisLayoutFlags.RightStick] =
            [
                CreateAxisAction(AxisLayoutFlags.RightPad),
                CreateButtonAction(ButtonFlags.RightPadTouch, Utils.DeflectionDirection.Any, 1000)
            ];

            return template;
        }

        private static ButtonActions CreateButtonAction(ButtonFlags button, Utils.DeflectionDirection motionDirection = Utils.DeflectionDirection.None, float motionThreshold = 4000)
        {
            return new ButtonActions
            {
                Button = button,
                motionDirection = motionDirection,
                motionThreshold = motionThreshold,
            };
        }

        private static KeyboardActions CreateKeyboardAction(VirtualKeyCode key, ModifierSet modifiers = ModifierSet.None, Utils.DeflectionDirection motionDirection = Utils.DeflectionDirection.None, float motionThreshold = 4000)
        {
            return new KeyboardActions
            {
                Key = key,
                Modifiers = modifiers,
                motionDirection = motionDirection,
                motionThreshold = motionThreshold,
            };
        }

        private static MouseActions CreateMouseAction(MouseActionsType mouseType, ModifierSet modifiers = ModifierSet.None, Utils.DeflectionDirection motionDirection = Utils.DeflectionDirection.None, float motionThreshold = 4000)
        {
            return new MouseActions
            {
                MouseType = mouseType,
                Modifiers = modifiers,
                motionDirection = motionDirection,
                motionThreshold = motionThreshold,
            };
        }

        private static AxisActions CreateAxisAction(AxisLayoutFlags axis, short x = 0, short y = 0)
        {
            return new AxisActions
            {
                Axis = axis,
                ButtonX = x,
                ButtonY = y,
            };
        }

        private LayoutTemplate(string name, string description, string author, bool isInternal, string? deviceName = null) : this()
        {
            Name = name;
            Description = description;
            Author = author;
            Product = string.Empty;

            IsInternal = isInternal;
            DeviceName = deviceName;

            Layout = new Layout();
            Layout.FillDefault();
        }

        private LayoutTemplate(string templateName, string author, bool isInternal, string? deviceName = null) :
            this(TranslationSource.Instance[$"LayoutTemplate_{templateName}"], TranslationSource.Instance[$"LayoutTemplate_{templateName}Desc"], author, isInternal, deviceName)
        {
            switch (templateName)
            {
                default:
                case "Default":
                    break;

                case "Desktop":
                    {
                        Layout.AxisLayout = new()
                        {
                            { AxisLayoutFlags.LeftStick, new List<IActions>() { CreateMouseAction(MouseActionsType.Scroll) } },
                            { AxisLayoutFlags.RightStick, new List<IActions>() { CreateMouseAction(MouseActionsType.Move) } },
                            { AxisLayoutFlags.LeftPad, new List<IActions>() { CreateMouseAction(MouseActionsType.Scroll) } },
                            { AxisLayoutFlags.RightPad, new List<IActions>() { CreateMouseAction(MouseActionsType.Move) } },
                            {
                                AxisLayoutFlags.L2, new List<IActions>()
                                {
                                    CreateMouseAction(MouseActionsType.RightButton, motionDirection: Utils.DeflectionDirection.Up, motionThreshold: Gamepad.TriggerThreshold)
                                }
                            },
                            {
                                AxisLayoutFlags.R2, new List<IActions>()
                                {
                                    CreateMouseAction(MouseActionsType.LeftButton, motionDirection: Utils.DeflectionDirection.Up, motionThreshold: Gamepad.TriggerThreshold)
                                }
                            }
                        };

                        Layout.ButtonLayout = new()
                        {
                            { ButtonFlags.B1, new List<IActions>() { CreateKeyboardAction(VirtualKeyCode.RETURN) } },
                            { ButtonFlags.B2, new List<IActions>() { CreateKeyboardAction(VirtualKeyCode.ESCAPE) } },
                            { ButtonFlags.B3, new List<IActions>() { CreateKeyboardAction(VirtualKeyCode.PRIOR) } },
                            { ButtonFlags.B4, new List<IActions>() { CreateKeyboardAction(VirtualKeyCode.NEXT) } },

                            { ButtonFlags.L1, new List<IActions>() { CreateKeyboardAction(VirtualKeyCode.BACK) } },
                            { ButtonFlags.R1, new List<IActions>() { CreateKeyboardAction(VirtualKeyCode.SPACE) } },

                            { ButtonFlags.Back, new List<IActions> { CreateKeyboardAction(VirtualKeyCode.TAB, ModifierSet.Alt) } },
                            { ButtonFlags.Start, new List<IActions>() { CreateKeyboardAction(VirtualKeyCode.TAB) } },

                            { ButtonFlags.DPadUp, new List<IActions>() { CreateKeyboardAction(VirtualKeyCode.UP) } },
                            { ButtonFlags.DPadDown, new List<IActions>() { CreateKeyboardAction(VirtualKeyCode.DOWN) } },
                            { ButtonFlags.DPadLeft, new List<IActions>() { CreateKeyboardAction(VirtualKeyCode.LEFT) } },
                            { ButtonFlags.DPadRight, new List<IActions>() { CreateKeyboardAction(VirtualKeyCode.RIGHT) } },

                            { ButtonFlags.LeftPadClick, new List<IActions>() { CreateMouseAction(MouseActionsType.RightButton) } },
                            { ButtonFlags.RightPadClick, new List<IActions>() { CreateMouseAction(MouseActionsType.LeftButton) } }
                        };
                    }
                    break;

                case "Nintendo":
                    {
                        Layout.ButtonLayout[ButtonFlags.B1] = [CreateButtonAction(ButtonFlags.B2)];
                        Layout.ButtonLayout[ButtonFlags.B2] = [CreateButtonAction(ButtonFlags.B1)];
                        Layout.ButtonLayout[ButtonFlags.B3] = [CreateButtonAction(ButtonFlags.B4)];
                        Layout.ButtonLayout[ButtonFlags.B4] = [CreateButtonAction(ButtonFlags.B3)];
                    }
                    break;

                case "Keyboard":
                    {
                        Layout.AxisLayout = new()
                        {
                            { AxisLayoutFlags.RightStick, new List<IActions>() { CreateMouseAction(MouseActionsType.Move) } },
                            { AxisLayoutFlags.RightPad, new List<IActions>() { CreateMouseAction(MouseActionsType.Move) } },
                            {
                                AxisLayoutFlags.LeftStick, new List<IActions>()
                                {
                                    CreateKeyboardAction(VirtualKeyCode.VK_A, motionDirection: Utils.DeflectionDirection.Left, motionThreshold: Gamepad.LeftThumbDeadZone),
                                    CreateKeyboardAction(VirtualKeyCode.VK_D, motionDirection: Utils.DeflectionDirection.Right, motionThreshold: Gamepad.LeftThumbDeadZone),
                                    CreateKeyboardAction(VirtualKeyCode.VK_W, motionDirection: Utils.DeflectionDirection.Up, motionThreshold: Gamepad.LeftThumbDeadZone),
                                    CreateKeyboardAction(VirtualKeyCode.VK_S, motionDirection: Utils.DeflectionDirection.Down, motionThreshold: Gamepad.LeftThumbDeadZone)
                                }
                            },
                            {
                                AxisLayoutFlags.L2, new List<IActions>()
                                {
                                    CreateMouseAction(MouseActionsType.RightButton, motionDirection: Utils.DeflectionDirection.Up, motionThreshold: Gamepad.TriggerThreshold)
                                }
                            },
                            {
                                AxisLayoutFlags.R2, new List<IActions>()
                                {
                                    CreateMouseAction(MouseActionsType.LeftButton, motionDirection: Utils.DeflectionDirection.Up, motionThreshold: Gamepad.TriggerThreshold)
                                }
                            }
                        };

                        Layout.ButtonLayout = new()
                        {
                            { ButtonFlags.B1, new List<IActions>() { CreateKeyboardAction(VirtualKeyCode.SPACE) } },
                            { ButtonFlags.B2, new List<IActions>() { CreateKeyboardAction(VirtualKeyCode.VK_E) } },
                            { ButtonFlags.B3, new List<IActions>() { CreateKeyboardAction(VirtualKeyCode.VK_R) } },
                            { ButtonFlags.B4, new List<IActions>() { CreateKeyboardAction(VirtualKeyCode.VK_F) } },

                            { ButtonFlags.L1, new List<IActions>() { CreateMouseAction(MouseActionsType.ScrollDown) } },
                            { ButtonFlags.R1, new List<IActions>() { CreateMouseAction(MouseActionsType.ScrollUp) } },

                            { ButtonFlags.Back, new List<IActions>() { CreateKeyboardAction(VirtualKeyCode.TAB) } },
                            { ButtonFlags.Start, new List<IActions>() { CreateKeyboardAction(VirtualKeyCode.ESCAPE) } },

                            { ButtonFlags.DPadUp, new List<IActions>() { CreateKeyboardAction(VirtualKeyCode.VK_1) } },
                            { ButtonFlags.DPadDown, new List<IActions>() { CreateKeyboardAction(VirtualKeyCode.VK_3) } },
                            { ButtonFlags.DPadLeft, new List<IActions>() { CreateKeyboardAction(VirtualKeyCode.VK_4) } },
                            { ButtonFlags.DPadRight, new List<IActions>() { CreateKeyboardAction(VirtualKeyCode.VK_2) } },

                            { ButtonFlags.LeftStickClick, new List<IActions>() { CreateKeyboardAction(VirtualKeyCode.LSHIFT) } },
                            { ButtonFlags.RightStickClick, new List<IActions>() { CreateMouseAction(MouseActionsType.LeftButton) } },

                            { ButtonFlags.LeftPadClickUp, new List<IActions>() { CreateKeyboardAction(VirtualKeyCode.VK_1) } },
                            { ButtonFlags.LeftPadClickDown, new List<IActions>() { CreateKeyboardAction(VirtualKeyCode.VK_3) } },
                            { ButtonFlags.LeftPadClickLeft, new List<IActions>() { CreateKeyboardAction(VirtualKeyCode.VK_4) } },
                            { ButtonFlags.LeftPadClickRight, new List<IActions>() { CreateKeyboardAction(VirtualKeyCode.VK_2) } },

                            { ButtonFlags.RightPadClick, new List<IActions>() { CreateMouseAction(MouseActionsType.LeftButton) } }
                        };
                    }
                    break;

                case "GamepadMouse":
                    {
                        Layout.AxisLayout[AxisLayoutFlags.RightPad] = new List<IActions>() { CreateMouseAction(MouseActionsType.Move) };
                    }
                    break;

                case "GamepadJoystick":
                    {
                        Layout.AxisLayout[AxisLayoutFlags.RightPad] = new List<IActions>() { CreateAxisAction(AxisLayoutFlags.RightStick) };
                    }
                    break;
            }
        }

        [JsonProperty] public string Author { get; set; } = string.Empty;
        [JsonProperty] public string Name { get; set; } = string.Empty;
        [JsonProperty] public string Description { get; set; } = string.Empty;
        [JsonProperty] public string Product { get; set; } = string.Empty;
        [JsonProperty] public Guid Guid { get; set; } = Guid.NewGuid();
        [JsonProperty] public string Executable { get; set; } = string.Empty;
        [JsonProperty] public bool IsInternal { get; set; } = false;
        [JsonProperty] public Layout Layout { get; set; } = new();

        [Obsolete("Use DeviceName instead. This property is kept for backward compatibility with older serialized templates.")]
        [JsonProperty("ControllerType")]
        [JsonConverter(typeof(ControllerTypeJsonConverter))]
        public Type? ControllerType { get; set; }

        [JsonProperty]
        public string? DeviceName { get; set; }

        public int CompareTo(object? obj)
        {
            if (obj is not LayoutTemplate profile)
                return 1;

            return profile.Name.CompareTo(Name);
        }

        public void ClearDelegates()
        {
            Updated = null;
        }

        private void Layout_Updated(Layout layout)
        {
            Updated?.Invoke(this);
        }

        #region events

        public event UpdatedEventHandler? Updated;

        public delegate void UpdatedEventHandler(LayoutTemplate layoutTemplate);

        #endregion
    }
}
