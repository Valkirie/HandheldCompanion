using ControllerCommon;
using ControllerCommon.Actions;
using ControllerCommon.Controllers;
using ControllerCommon.Inputs;
using GregsStack.InputSimulatorStandard.Native;
using HandheldCompanion.Actions;
using System;
using static HandheldCompanion.Simulators.MouseSimulator;

namespace HandheldCompanion.Managers
{
    static class MappingManager
    {
        private static Profile currentProfile;

        private static ButtonState prevButtonState = new();
        private static AxisState prevAxisState = new();

        static MappingManager()
        {
            ProcessManager.ForegroundChanged += ProcessManager_ForegroundChanged;
        }

        private static void ProcessManager_ForegroundChanged(ProcessEx processEx, ProcessEx backgroundEx)
        {
            currentProfile = ProfileManager.GetProfileFromExec(processEx.Executable);
            if (currentProfile is null)
                currentProfile = ProfileManager.GetDefault();
        }

        public static void SetAction(ButtonFlags button, IActions action)
        {
            currentProfile.ButtonMapping[button] = action;
        }

        public static bool HasAction(ButtonFlags button)
        {
            return currentProfile.ButtonMapping.ContainsKey(button);
        }

        public static IActions GetAction(ButtonFlags button)
        {
            return currentProfile.ButtonMapping[button];
        }

        public static ControllerState MapController(ControllerState controllerState)
        {
            if (currentProfile is null)
                return controllerState;

            // create output state
            ControllerState outputState = controllerState.Clone() as ControllerState;

            foreach (ButtonFlags button in controllerState.ButtonState.Buttons)
            {
                // consume origin button state
                if (currentProfile.ButtonMapping.ContainsKey(button))
                    outputState.ButtonState[button] = false;
            }

            // if (!prevButtonState.Equals(controllerState.ButtonState))
            foreach (var buttonState in controllerState.ButtonState.State)
            {
                ButtonFlags button = buttonState.Key;
                bool status = buttonState.Value;

                // skip, if not mapped
                if (!currentProfile.ButtonMapping.ContainsKey(button))
                    continue;

                // pull action
                IActions action = currentProfile.ButtonMapping[button];
                switch (action.ActionType)
                {
                    // button to button
                    case ActionType.Button:
                        {
                            ButtonActions bAction = action as ButtonActions;
                            
                            outputState.ButtonState[bAction.Button] |= status;
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

            if (true) // if (!prevAxisState.Equals(controllerState.AxisState))
            {
                foreach (var axisState in controllerState.AxisState.State)
                {
                    AxisFlags axis = axisState.Key;
                    int value = axisState.Value;
                    bool below_deadzone = Math.Abs(value) <= ControllerState.AxisDeadzones[axis];

                    // skip if not mapped
                    if (!currentProfile.AxisMapping.ContainsKey(axis))
                        continue;

                    // consume axis
                    if (!outputState.AxisState.Emulated[axis])
                        outputState.AxisState[axis] = 0;

                    // pull action
                    IActions action = currentProfile.AxisMapping[axis];

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
            }

            /*
            prevButtonState = controllerState.ButtonState.Clone() as ButtonState;
            prevAxisState = controllerState.AxisState.Clone() as AxisState;
            */

            return outputState;
        }
    }
}
