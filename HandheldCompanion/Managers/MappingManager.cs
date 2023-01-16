using ControllerCommon.Controllers;
using ControllerCommon.Inputs;
using GregsStack.InputSimulatorStandard.Native;
using HandheldCompanion.Actions;
using System;
using System.Collections.Generic;
using System.Windows.Forms;
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
            ButtonMapping.Add(ButtonFlags.B1, new ButtonActions(ButtonFlags.B2));
            ButtonMapping.Add(ButtonFlags.B2, new ButtonActions(ButtonFlags.B1));

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
                outputState.ButtonState[button] = false;

                // pull action
                IActions action = ButtonMapping[button];

                switch(action.ActionType)
                {
                    // button to button
                    case ActionType.Button:
                        {
                            // inject button
                            ButtonActions bAction = action as ButtonActions;
                            outputState.ButtonState[bAction.Button] = status;
                        }
                        break;

                    // button to axis
                    case ActionType.Axis:
                        {
                            // inject axis
                            AxisActions aAction = action as AxisActions;
                            outputState.AxisState[aAction.Axis] = aAction.Value;
                        }
                        break;
                    
                    // button to keyboard key
                    case ActionType.Keyboard:
                        {
                            // inject keyboard key
                            KeyboardActions kAction = action as KeyboardActions;
                            kAction.Execute(button, status);
                        }
                        break;

                    // button to mouse click
                    case ActionType.Mouse:
                        {
                            // inject mouse click
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

                // skip if not mapped
                if (!AxisMapping.ContainsKey(axis))
                    continue;

                // skip if below deadzone
                if (Math.Abs(value) < ControllerState.AxisDeadzones[axis])
                    continue;

                // consume axis
                outputState.AxisState[axis] = 0;

                // pull action
                IActions action = AxisMapping[axis];

                switch (action.ActionType)
                {
                    // axis to button
                    case ActionType.Button:
                        {
                        }
                        break;

                    // axis to axis
                    case ActionType.Axis:
                        {
                        }
                        break;

                    // axis to keyboard key
                    case ActionType.Keyboard:
                        {
                        }
                        break;

                    // axis to mouse movements
                    case ActionType.Mouse:
                        {
                            // inject mouse movements
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
