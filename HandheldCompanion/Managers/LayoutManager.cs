using ControllerCommon;
using ControllerCommon.Actions;
using ControllerCommon.Controllers;
using ControllerCommon.Inputs;
using ControllerCommon.Managers;
using Gma.System.MouseKeyHook.HotKeys;
using GregsStack.InputSimulatorStandard.Native;
using HandheldCompanion.Actions;
using Newtonsoft.Json;
using System;
using System.Globalization;
using System.IO;
using static HandheldCompanion.Simulators.MouseSimulator;

namespace HandheldCompanion.Managers
{
    static class LayoutManager
    {
        public static Layout desktopLayout = new("Desktop");
        public static Layout profileLayout = new();
        private static Layout currentLayout;

        private static ControllerState outputState = new();
        private static ButtonState prevButtonState = new();
        private static AxisState prevAxisState = new();

        public static string InstallPath;
        private static bool IsInitialized;

        static LayoutManager()
        {
            // initialiaze path
            InstallPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "HandheldCompanion", "layouts");
            if (!Directory.Exists(InstallPath))
                Directory.CreateDirectory(InstallPath);

            // process existing profiles
            string[] fileEntries = Directory.GetFiles(InstallPath, "*.json", SearchOption.AllDirectories);
            foreach (string fileName in fileEntries)
                ProcessLayout(fileName);

            desktopLayout.Updated += DesktopLayout_Updated;

            ProcessManager.ForegroundChanged += ProcessManager_ForegroundChanged;
            SettingsManager.SettingValueChanged += SettingsManager_SettingValueChanged;
        }

        private static void ProcessLayout(string fileName)
        {
            Layout layout = null;
            try
            {
                string outputraw = File.ReadAllText(fileName);
                layout = JsonConvert.DeserializeObject<Layout>(outputraw, new JsonSerializerSettings
                {
                    TypeNameHandling = TypeNameHandling.All
                });
            }
            catch (Exception ex)
            {
                LogManager.LogError("Could not parse layout {0}. {1}", fileName, ex.Message);
            }

            // failed to parse
            if (layout is null || layout.ButtonLayout is null || layout.AxisLayout is null)
            {
                LogManager.LogError("Could not parse layout {0}", fileName);
                return;
            }
            
            // update current layout
            // todo: support multiple layouts when non-gaming ?
            desktopLayout = layout;
            desktopLayout.Updated += DesktopLayout_Updated;
        }

        private static void DesktopLayout_Updated(Layout layout)
        {
            SerializeLayout(layout);
        }

        private static void SerializeLayout(Layout layout)
        {
            string jsonString = JsonConvert.SerializeObject(layout, Formatting.Indented, new JsonSerializerSettings
            {
                TypeNameHandling = TypeNameHandling.All
            });

            string settingsPath = Path.Combine(InstallPath, $"{layout.Name}.json");
            File.WriteAllText(settingsPath, jsonString);
        }

        private static void SettingsManager_SettingValueChanged(string name, object value)
        {
            switch (name)
            {
                case "shortcutDesktopLayout":
                    {
                        bool toggle = Convert.ToBoolean(value);
                        switch(toggle)
                        {
                            case true:
                                currentLayout = desktopLayout;
                                break;
                            case false:
                                currentLayout = profileLayout;
                                break;
                        }
                    }
                    break;
            }
        }

        private static void ProcessManager_ForegroundChanged(ProcessEx processEx, ProcessEx backgroundEx)
        {
            var profile = ProfileManager.GetProfileFromExec(processEx.Executable);
            profileLayout = profile.Layout;

            // only update current layout if we're not into desktop layout mode
            if (!SettingsManager.GetBoolean("shortcutDesktopLayout", true))
                currentLayout = profile.Layout;
        }

        public static ControllerState MapController(ControllerState controllerState)
        {
            if (currentLayout is null)
                return controllerState;

            // consume origin button state
            outputState.ButtonState = controllerState.ButtonState.Clone() as ButtonState;
            foreach (ButtonFlags button in currentLayout.ButtonLayout.Keys)
                outputState.ButtonState[button] = false;

            // consume origin axis state
            outputState.AxisState = controllerState.AxisState.Clone() as AxisState;
            foreach (AxisFlags axis in currentLayout.AxisLayout.Keys)
                outputState.AxisState[axis] = 0;

            foreach (var buttonState in controllerState.ButtonState.State)
            {
                ButtonFlags button = buttonState.Key;
                bool value = buttonState.Value;

                // skip, if not mapped
                if (!currentLayout.ButtonLayout.ContainsKey(button))
                    continue;

                // pull action
                IActions action = currentLayout.ButtonLayout[button];
                switch (action.ActionType)
                {
                    // button to button
                    case ActionType.Button:
                        {
                            ButtonActions bAction = action as ButtonActions;
                            value |= outputState.ButtonState[bAction.Button];

                            bAction.Execute(button, value);
                            outputState.ButtonState[bAction.Button] = bAction.GetValue();
                        }
                        break;

                    // button to axis
                    case ActionType.Axis:
                        {
                            if (!value)
                                continue;

                            AxisActions aAction = action as AxisActions;
                            aAction.Execute(button, value);

                            outputState.AxisState[aAction.Axis] = aAction.GetValue();
                        }
                        break;

                    // button to keyboard key
                    case ActionType.Keyboard:
                        {
                            KeyboardActions kAction = action as KeyboardActions;
                            kAction.Execute(button, value);
                        }
                        break;

                    // button to mouse click
                    case ActionType.Mouse:
                        {
                            MouseActions mAction = action as MouseActions;
                            mAction.Execute(button, value);
                        }
                        break;
                }
            }

            foreach (var axisState in controllerState.AxisState.State)
            {
                AxisFlags axis = axisState.Key;
                short value = axisState.Value;
                bool below_deadzone = Math.Abs(value) <= ControllerState.AxisDeadzones[axis];

                // skip, if not mapped
                if (!currentLayout.AxisLayout.ContainsKey(axis))
                    continue;

                // pull action
                IActions action = currentLayout.AxisLayout[axis];
                switch (action.ActionType)
                {
                    // axis to button
                    case ActionType.Button:
                        {
                            if (below_deadzone)
                                break;

                            ButtonActions bAction = action as ButtonActions;
                            outputState.ButtonState[bAction.Button] = !below_deadzone;
                        }
                        break;

                    // axis to axis
                    case ActionType.Axis:
                        {
                            AxisActions aAction = action as AxisActions;
                            aAction.Execute(axis, value);

                            outputState.AxisState[aAction.Axis] = aAction.GetValue();
                        }
                        break;

                    // axis to keyboard key
                    case ActionType.Keyboard:
                        {
                            if (below_deadzone)
                                break;

                            KeyboardActions kAction = action as KeyboardActions;
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
