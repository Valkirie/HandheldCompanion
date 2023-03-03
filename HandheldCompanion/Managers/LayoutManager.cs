using ControllerCommon;
using ControllerCommon.Actions;
using ControllerCommon.Controllers;
using ControllerCommon.Inputs;
using ControllerCommon.Managers;
using ControllerCommon.Utils;
using GregsStack.InputSimulatorStandard.Native;
using HandheldCompanion.Actions;
using HandheldCompanion.Managers.Layouts;
using LiveCharts.Wpf;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Documents;
using static HandheldCompanion.Simulators.MouseSimulator;
using Vector2 = System.Numerics.Vector2;

namespace HandheldCompanion.Managers
{
    static class LayoutManager
    {
        public static Layout desktopLayout = LayoutTemplates.DesktopLayout;
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
                        switch (toggle)
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

            outputState.ButtonState = controllerState.ButtonState.Clone() as ButtonState;
            outputState.AxisState = controllerState.AxisState.Clone() as AxisState;

            // consume origin button state
            foreach (ButtonFlags button in currentLayout.ButtonLayout.Keys)
                outputState.ButtonState[button] = false;

            // consume origin axis state
            foreach (var axisLayout in currentLayout.AxisLayout)
            {
                AxisLayoutFlags flags = axisLayout.Key;

                // read origin values
                AxisLayout InLayout = AxisLayout.Layouts[flags];
                AxisFlags InAxisX = InLayout.GetAxisFlags('X');
                AxisFlags InAxisY = InLayout.GetAxisFlags('Y');

                outputState.AxisState[InAxisX] = 0;
                outputState.AxisState[InAxisY] = 0;
            }
            
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

            foreach (var axisLayout in currentLayout.AxisLayout)
            {
                AxisLayoutFlags flags = axisLayout.Key;

                // read origin values
                AxisLayout InLayout = AxisLayout.Layouts[flags];
                AxisFlags InAxisX = InLayout.GetAxisFlags('X');
                AxisFlags InAxisY = InLayout.GetAxisFlags('Y');

                InLayout.vector.X = controllerState.AxisState[InAxisX];
                InLayout.vector.Y = controllerState.AxisState[InAxisY];

                // pull action
                IActions action = axisLayout.Value;
                switch (action.ActionType)
                {
                    case ActionType.Joystick:
                        {
                            AxisActions aAction = action as AxisActions;
                            aAction.Execute(InLayout);

                            // read output axis
                            AxisLayout OutLayout = AxisLayout.Layouts[aAction.Axis];
                            AxisFlags OutAxisX = OutLayout.GetAxisFlags('X');
                            AxisFlags OutAxisY = OutLayout.GetAxisFlags('Y');

                            outputState.AxisState[OutAxisX] = (short)aAction.GetValue().X;
                            outputState.AxisState[OutAxisY] = (short)aAction.GetValue().Y;
                        }
                        break;

                    case ActionType.Mouse:
                        {
                            MouseActions mAction = action as MouseActions;
                            mAction.Execute(InLayout);
                        }
                        break;

                    case ActionType.Trigger:
                        {
                            TriggerActions tAction = action as TriggerActions;
                            tAction.Execute(InAxisY, (short)InLayout.vector.Y);

                            // read output axis
                            AxisLayout OutLayout = AxisLayout.Layouts[tAction.Axis];
                            AxisFlags OutAxisY = OutLayout.GetAxisFlags('Y');

                            outputState.AxisState[OutAxisY] = (short)tAction.GetValue();
                        }
                        break;
                }
            }

            return outputState;
        }
    }
}
