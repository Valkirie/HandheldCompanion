using ControllerCommon.Controllers;
using ControllerCommon.Inputs;
using GregsStack.InputSimulatorStandard.Native;
using HandheldCompanion.Actions;
using System;
using System.Collections.Generic;
using static HandheldCompanion.Simulators.MouseSimulator;
using static System.Windows.Forms.VisualStyles.VisualStyleElement;

namespace HandheldCompanion.Managers
{
    static class MappingManager
    {
        private static Dictionary<ButtonFlags, IActions> ButtonMapping = new();
        private static Dictionary<AxisFlags, IActions> AxisMapping = new();

        static MappingManager()
        {
            // XBOX to NINTENDO
            ButtonMapping.Add(ButtonFlags.B1, new ButtonActions(ButtonFlags.B2));
            ButtonMapping.Add(ButtonFlags.B2, new ButtonActions(ButtonFlags.B1));
            ButtonMapping.Add(ButtonFlags.B3, new ButtonActions(ButtonFlags.B4));
            ButtonMapping.Add(ButtonFlags.B4, new ButtonActions(ButtonFlags.B3));

            ButtonMapping.Add(ButtonFlags.DPadLeft, new AxisActions(AxisFlags.LeftThumbX, short.MinValue));
            ButtonMapping.Add(ButtonFlags.DPadRight, new AxisActions(AxisFlags.LeftThumbX, short.MaxValue));

            ButtonMapping.Add(ButtonFlags.LStickUp, new KeyboardActions(VirtualKeyCode.VK_W));
            ButtonMapping.Add(ButtonFlags.LStickDown, new KeyboardActions(VirtualKeyCode.VK_S));
            ButtonMapping.Add(ButtonFlags.LStickLeft, new KeyboardActions(VirtualKeyCode.VK_A));
            ButtonMapping.Add(ButtonFlags.LStickRight, new KeyboardActions(VirtualKeyCode.VK_D));

            ButtonMapping.Add(ButtonFlags.L1, new MouseActions(MouseActionsType.LeftButton));
            ButtonMapping.Add(ButtonFlags.R1, new MouseActions(MouseActionsType.RightButton));

            AxisMapping.Add(AxisFlags.RightThumbX, new MouseActions(MouseActionsType.MoveByX));
            AxisMapping.Add(AxisFlags.RightThumbY, new MouseActions(MouseActionsType.MoveByY));

            AxisMapping.Add(AxisFlags.LeftThumbX, new AxisActions(AxisFlags.RightThumbX));
            AxisMapping.Add(AxisFlags.LeftThumbY, new AxisActions(AxisFlags.RightThumbY));
        }

        public static void SetAction(ButtonFlags button, IActions action)
        {
            ButtonMapping[button] = action;
        }

        public static bool HasAction(ButtonFlags button)
        {
            return ButtonMapping.ContainsKey(button);
        }

        public static IActions GetAction(ButtonFlags button)
        {
            return ButtonMapping[button];
        }

        public static ControllerState MapController(ControllerState controllerState)
        {
            ControllerState outputState = controllerState.Clone() as ControllerState;

            foreach (var buttonState in controllerState.ButtonState.State)
            {
                ButtonFlags button = buttonState.Key;
                bool status = buttonState.Value;

                // skip if not mapped
                if (!ButtonMapping.ContainsKey(button))
                    continue;

                // consume button
                if (!outputState.ButtonState.Emulated[button])
                    outputState.ButtonState[button] = false;

                    // pull action
                    IActions action = ButtonMapping[button];

                switch(action.ActionType)
                {
                    // button to button
                    case ActionType.Button:
                        {
                            ButtonActions bAction = action as ButtonActions;
                            outputState.ButtonState[bAction.Button] = status;
                            outputState.ButtonState.Emulated[bAction.Button] = true;
                        }
                        break;

                    // button to axis
                    case ActionType.Axis:
                        {
                            if (!status)
                                continue;

                            AxisActions aAction = action as AxisActions;
                            outputState.AxisState[aAction.Axis] = aAction.Value;
                            outputState.AxisState.Emulated[aAction.Axis] = true;
                        }
                        break;
                    
                    // button to keyboard key
                    case ActionType.Keyboard:
                        {
                            KeyboardActions kAction = action as KeyboardActions;
                            kAction.Execute(button, status);
                        }
                        break;

                    // button to mouse click
                    case ActionType.Mouse:
                        {
                            MouseActions mAction = action as MouseActions;
                            mAction.Execute(button, status);
                        }
                        break;
                }
            }

            foreach(var axisState in controllerState.AxisState.State)
            {
                AxisFlags axis = axisState.Key;
                int value = axisState.Value;
                bool below_deadzone = Math.Abs(value) <= ControllerState.AxisDeadzones[axis];

                // skip if not mapped
                if (!AxisMapping.ContainsKey(axis))
                    continue;

                // consume axis
                if (!outputState.AxisState.Emulated[axis])
                    outputState.AxisState[axis] = 0;

                // pull action
                IActions action = AxisMapping[axis];

                switch (action.ActionType)
                {
                    // axis to button
                    case ActionType.Button:
                        {
                            if (below_deadzone)
                                break;

                            ButtonActions bAction = action as ButtonActions;
                            outputState.ButtonState[bAction.Button] = !below_deadzone;
                            outputState.ButtonState.Emulated[bAction.Button] = true;
                        }
                        break;

                    // axis to axis
                    case ActionType.Axis:
                        {
                            AxisActions aAction = action as AxisActions;
                            outputState.AxisState[aAction.Axis] = (short)value;
                            outputState.AxisState.Emulated[aAction.Axis] = true;
                        }
                        break;

                    // axis to keyboard key
                    case ActionType.Keyboard:
                        {
                            if (below_deadzone)
                                break;

                            KeyboardActions kAction = action as KeyboardActions;
                            kAction.Execute(axis, !below_deadzone);
                        }
                        break;

                    // axis to mouse movements
                    case ActionType.Mouse:
                        {
                            if (below_deadzone)
                                break;

                            MouseActions mAction = action as MouseActions;
                            mAction.Execute(axis, (short)value);
                        }
                        break;
                }

            }

            return outputState;
        }
    }
}
