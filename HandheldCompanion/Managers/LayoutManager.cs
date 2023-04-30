using ControllerCommon;
using ControllerCommon.Actions;
using ControllerCommon.Controllers;
using ControllerCommon.Inputs;
using ControllerCommon.Managers;
using HandheldCompanion.Actions;
using HandheldCompanion.Controls;
using HandheldCompanion.Views;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
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
        };

        private static Layout currentLayout;

        public static FileSystemWatcher layoutWatcher { get; set; }

        public static string LayoutsPath;
        public static string TemplatesPath;

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
            LayoutsPath = Path.Combine(MainWindow.SettingsPath, "layouts");
            if (!Directory.Exists(LayoutsPath))
                Directory.CreateDirectory(LayoutsPath);

            TemplatesPath = Path.Combine(MainWindow.SettingsPath, "templates");
            if (!Directory.Exists(TemplatesPath))
                Directory.CreateDirectory(TemplatesPath);

            // monitor layout files
            layoutWatcher = new FileSystemWatcher()
            {
                Path = LayoutsPath,
                EnableRaisingEvents = true,
                IncludeSubdirectories = true,
                Filter = "*.json",
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.Size
            };

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

            // process community layouts
            List<string> fileEntries = new();
            fileEntries.AddRange(Directory.GetFiles(LayoutsPath, "*.json", SearchOption.AllDirectories));
            fileEntries.AddRange(Directory.GetFiles(TemplatesPath, "*.json", SearchOption.AllDirectories));

            foreach (string fileName in fileEntries)
                ProcessLayoutTemplate(fileName);

            // process template layouts
            foreach (LayoutTemplate layoutTemplate in Templates)
                Updated?.Invoke(layoutTemplate);

            layoutWatcher.Created += LayoutWatcher_Created;

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

        private static void LayoutWatcher_Created(object sender, FileSystemEventArgs e)
        {
            ProcessLayoutTemplate(e.FullPath);
        }

        private static bool LayoutTemplateExist(LayoutTemplate layoutTemplate)
        {
            string fileName = Path.Combine(TemplatesPath, $"{layoutTemplate.Name}.json");
            return File.Exists(fileName);
        }

        private static void ProcessLayoutTemplate(string fileName)
        {
            // UI thread (synchronous)
            // We need to wait for each controller to initialize and take (or not) its slot in the array
            Application.Current.Dispatcher.Invoke(() =>
            {
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
                        // if we need to make this function async, then we need to check below if we're in desktop mode
                        // if yes, update currentLayout with desktopLayout
                        desktopLayout = layoutTemplate;
                        desktopLayout.Updated += LayoutTemplate_Updated;
                        break;

                    default:
                        // todo: implement deduplication
                        Templates.Add(layoutTemplate);
                        break;
                }

                Updated?.Invoke(layoutTemplate);
            });
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
            if (isCurrent && (profile.ErrorCode.HasFlag(ProfileErrorCode.Running & ProfileErrorCode.Default)))
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

        public static Layout GetCurrent()
        {
            return currentLayout;
        }

        public static void SerializeLayoutTemplate(LayoutTemplate layoutTemplate)
        {
            string jsonString = JsonConvert.SerializeObject(layoutTemplate, Formatting.Indented, new JsonSerializerSettings
            {
                TypeNameHandling = TypeNameHandling.All
            });

            string fileName = string.Empty;

            if (layoutTemplate.IsTemplate)
                fileName = Path.Combine(TemplatesPath, $"{layoutTemplate.Name}.json");
            else
                fileName = Path.Combine(LayoutsPath, $"{layoutTemplate.Name}_{layoutTemplate.Author}.json");

            File.WriteAllText(fileName, jsonString);
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

            // clean output state, there should be no leaking of current controller state,
            // only buttons/axes mapped from the layout should be passed on
            ControllerState outputState = new();

            // pass virtual buttons
            foreach(ButtonFlags button in IController.VirtualButtons)
                outputState.ButtonState[button] = controllerState.ButtonState[button];

            foreach (var buttonState in controllerState.ButtonState.State)
            {
                ButtonFlags button = buttonState.Key;
                bool value = buttonState.Value;

                // skip, if not mapped
                if (!currentLayout.ButtonLayout.TryGetValue(button, out IActions action))
                    continue;

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

                            outputState.AxisState[OutAxisX] = (short)(Math.Clamp(aAction.GetValue().X, short.MinValue, short.MaxValue));
                            outputState.AxisState[OutAxisY] = (short)(Math.Clamp(aAction.GetValue().Y, short.MinValue, short.MaxValue));
                        }
                        break;

                    case ActionType.Mouse:
                        {
                            MouseActions mAction = action as MouseActions;

                            // This buttonState check won't work here if UpdateInputs is event based, might need a rework in the future
                            bool touched = false;
                            if (ControllerState.AxisTouchButtons.TryGetValue(InLayout.flags, out ButtonFlags touchButton))
                                touched = controllerState.ButtonState[touchButton];

                            mAction.Execute(InLayout, touched);
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
