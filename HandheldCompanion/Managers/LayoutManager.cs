using ControllerCommon;
using ControllerCommon.Actions;
using ControllerCommon.Controllers;
using ControllerCommon.Inputs;
using ControllerCommon.Managers;
using HandheldCompanion.Actions;
using HandheldCompanion.Controls;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using Layout = ControllerCommon.Layout;

namespace HandheldCompanion.Managers
{
    static class LayoutManager
    {
        public static LayoutTemplate profileLayout = new();
        public static LayoutTemplate desktopLayout = LayoutTemplate.DesktopLayout;

        public static List<LayoutTemplate> Templates = new()
        {
            LayoutTemplate.DefaultLayout,
            LayoutTemplate.NintendoLayout,
            LayoutTemplate.KeyboardLayout,
            LayoutTemplate.GamepadMouseLayout,
            LayoutTemplate.GamepadJoystickLayout,
            LayoutTemplate.DesktopLayout,
        };

        private static Layout currentLayout;

        private static ControllerState outputState = new();

        public static string InstallPath;
        private static bool IsInitialized;

        #region events
        public static event InitializedEventHandler Initialized;
        public delegate void InitializedEventHandler();
        public static event UpdatedEventHandler Updated;
        public delegate void UpdatedEventHandler(LayoutTemplate layoutTemplate);
        #endregion

        static LayoutManager()
        {
            // initialiaze path
            InstallPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "HandheldCompanion", "layouts");
            if (!Directory.Exists(InstallPath))
                Directory.CreateDirectory(InstallPath);

            ProfileManager.Applied += ProfileManager_Applied;
            ProfileManager.Updated += ProfileManager_Updated;
            ProfileManager.Discarded += ProfileManager_Discarded;

            SettingsManager.SettingValueChanged += SettingsManager_SettingValueChanged;
        }

        public static void Start()
        {
            // generate template(s)
            if (!LayoutTemplateExist(desktopLayout))
                SerializeLayoutTemplate(desktopLayout);

            // process existing layouts
            string[] fileEntries = Directory.GetFiles(InstallPath, "*.json", SearchOption.AllDirectories);
            foreach (string fileName in fileEntries)
                ProcessLayoutTemplate(fileName);

            IsInitialized = true;
            Initialized?.Invoke();

            LogManager.LogInformation("{0} has started", "LayoutManager");
        }

        public static void Stop()
        {
            if (!IsInitialized)
                return;

            IsInitialized = false;

            LogManager.LogInformation("{0} has stopped", "LayoutManager");
        }

        private static bool LayoutTemplateExist(LayoutTemplate layoutTemplate)
        {
            string fileName = Path.Combine(InstallPath, $"{layoutTemplate.Name}.json");
            return File.Exists(fileName);
        }

        private static void ProcessLayoutTemplate(string fileName)
        {
            string layoutName = Path.GetFileNameWithoutExtension(fileName);

            // initialize value
            LayoutTemplate layoutTemplate = null;

            try
            {
                string outputraw = File.ReadAllText(fileName);
                layoutTemplate = JsonConvert.DeserializeObject<LayoutTemplate>(outputraw, new JsonSerializerSettings
                {
                    TypeNameHandling = TypeNameHandling.All
                });
            }
            catch (Exception ex)
            {
                LogManager.LogError("Could not parse LayoutTemplate {0}. {1}", fileName, ex.Message);
            }

            // failed to parse
            if (layoutTemplate is null || layoutTemplate.Layout is null)
            {
                LogManager.LogError("Could not parse LayoutTemplate {0}", fileName);
                return;
            }

            // create/update templates
            switch (layoutTemplate.Name)
            {
                case "Desktop":
                    desktopLayout = layoutTemplate;
                    desktopLayout.Updated += LayoutTemplate_Updated;
                    break;

                default:
                    Templates.Add(layoutTemplate);
                    break;
            }
        }

        private static void LayoutTemplate_Updated(LayoutTemplate layoutTemplate)
        {
            SerializeLayoutTemplate(layoutTemplate);
        }

        private static void ProfileManager_Applied(Profile profile)
        {
            UpdateCurrentLayout(profile);
        }

        private static void ProfileManager_Updated(Profile profile, ProfileUpdateSource source, bool isCurrent)
        {
            // ignore profile update if not current or not running
            if (isCurrent && profile.Running)
                UpdateCurrentLayout(profile);
        }

        private static void ProfileManager_Discarded(Profile profile, bool isCurrent, bool isUpdate)
        {
            // ignore discard signal if part of a profile switch
            if (!isUpdate)
                UpdateCurrentLayout();
        }

        private static void UpdateCurrentLayout(Profile profile = null)
        {
            Profile defaultProfile = ProfileManager.GetDefault();

            if (profile is not null && profile.LayoutEnabled && profile.Enabled)
                profileLayout.Layout = profile.Layout.Clone() as Layout;
            else if (defaultProfile is not null && defaultProfile.LayoutEnabled && defaultProfile.Enabled)
                profileLayout.Layout = defaultProfile.Layout.Clone() as Layout;
            else
                profileLayout.Layout = null;

            // only update current layout if we're not into desktop layout mode
            if (!SettingsManager.GetBoolean("shortcutDesktopLayout", true))
                currentLayout = profileLayout.Layout;
        }

        private static void SerializeLayoutTemplate(LayoutTemplate layoutTemplate)
        {
            string jsonString = JsonConvert.SerializeObject(layoutTemplate, Formatting.Indented, new JsonSerializerSettings
            {
                TypeNameHandling = TypeNameHandling.All
            });

            string settingsPath = Path.Combine(InstallPath, $"{layoutTemplate.Name}.json");
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
                                currentLayout = desktopLayout.Layout;
                                break;
                            case false:
                                currentLayout = profileLayout.Layout;
                                break;
                        }
                    }
                    break;
            }
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

                if (action is null)
                    continue;

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

                if (action is null)
                    continue;

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
