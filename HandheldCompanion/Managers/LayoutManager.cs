using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using ControllerCommon;
using ControllerCommon.Actions;
using ControllerCommon.Controllers;
using ControllerCommon.Inputs;
using ControllerCommon.Managers;
using HandheldCompanion.Actions;
using HandheldCompanion.Controls;
using HandheldCompanion.Managers.Desktop;
using HandheldCompanion.Views;
using Newtonsoft.Json;

namespace HandheldCompanion.Managers;

internal static class LayoutManager
{
    public static List<LayoutTemplate> Templates = new()
    {
        LayoutTemplate.DefaultLayout,
        LayoutTemplate.DesktopLayout,
        LayoutTemplate.NintendoLayout,
        LayoutTemplate.KeyboardLayout,
        LayoutTemplate.GamepadMouseLayout,
        LayoutTemplate.GamepadJoystickLayout
    };

    private static bool updateLock;
    private static Layout currentLayout;
    private static ScreenRotation currentOrientation = new();
    private static Layout profileLayout;
    private static Layout desktopLayout;
    private static readonly string desktopLayoutFile = "desktop";

    public static string LayoutsPath;
    public static string TemplatesPath;

    private static bool IsInitialized;

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
        layoutWatcher = new FileSystemWatcher
        {
            Path = TemplatesPath,
            EnableRaisingEvents = true,
            IncludeSubdirectories = true,
            Filter = "*.json",
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.Size
        };

        ProfileManager.Applied += ProfileManager_Applied;

        SettingsManager.SettingValueChanged += SettingsManager_SettingValueChanged;

        SystemManager.DisplayOrientationChanged += DesktopManager_DisplayOrientationChanged;
    }

    public static FileSystemWatcher layoutWatcher { get; set; }

    public static void Start()
    {
        // process community templates
        var fileEntries = Directory.GetFiles(TemplatesPath, "*.json", SearchOption.AllDirectories);
        foreach (var fileName in fileEntries)
            ProcessLayoutTemplate(fileName);

        foreach (var layoutTemplate in Templates)
            Updated?.Invoke(layoutTemplate);

        var desktopFile = Path.Combine(LayoutsPath, $"{desktopLayoutFile}.json");
        desktopLayout = ProcessLayout(desktopFile);
        if (desktopLayout is null)
        {
            desktopLayout = LayoutTemplate.DesktopLayout.Layout.Clone() as Layout;
            DesktopLayout_Updated(desktopLayout);
        }

        desktopLayout.Updated += DesktopLayout_Updated;

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

    private static Layout? ProcessLayout(string fileName)
    {
        // UI thread (synchronous)
        return Application.Current.Dispatcher.Invoke(() =>
        {
            Layout layout = null;

            try
            {
                var outputraw = File.ReadAllText(fileName);
                layout = JsonConvert.DeserializeObject<Layout>(outputraw, new JsonSerializerSettings
                {
                    TypeNameHandling = TypeNameHandling.All
                });
            }
            catch (Exception ex)
            {
                LogManager.LogError("Could not parse Layout {0}. {1}", fileName, ex.Message);
            }

            // failed to parse
            if (layout is null)
                LogManager.LogError("Could not parse Layout {0}", fileName);

            return layout;
        });
    }

    private static void ProcessLayoutTemplate(string fileName)
    {
        // UI thread (synchronous)
        Application.Current.Dispatcher.Invoke(() =>
        {
            LayoutTemplate layoutTemplate = null;

            try
            {
                var outputraw = File.ReadAllText(fileName);
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

            // todo: implement deduplication
            Templates.Add(layoutTemplate);
            Updated?.Invoke(layoutTemplate);
        });
    }

    private static void DesktopLayout_Updated(Layout layout)
    {
        SerializeLayout(layout, desktopLayoutFile);
    }

    private static void ProfileManager_Applied(Profile profile, ProfileUpdateSource source)
    {
        SetProfileLayout(profile);
    }

    private static void SetProfileLayout(Profile profile = null)
    {
        var defaultProfile = ProfileManager.GetDefault();

        if (profile.LayoutEnabled)
            // use profile layout if enabled
            profileLayout = profile.Layout.Clone() as Layout;
        else if (defaultProfile.LayoutEnabled)
            // fallback to default profile layout if enabled
            profileLayout = defaultProfile.Layout.Clone() as Layout;
        else
            // this should not happen, defaultProfile LayoutEnabled should always be true 
            profileLayout = null;

        // only update current layout if we're not into desktop layout mode
        if (!SettingsManager.GetBoolean("DesktopLayoutEnabled", true))
            SetActiveLayout(profileLayout);
    }

    public static Layout GetCurrent()
    {
        return currentLayout;
    }

    public static Layout GetDesktop()
    {
        return desktopLayout;
    }

    public static void SerializeLayout(Layout layout, string fileName)
    {
        var jsonString = JsonConvert.SerializeObject(layout, Formatting.Indented, new JsonSerializerSettings
        {
            TypeNameHandling = TypeNameHandling.All
        });

        fileName = Path.Combine(LayoutsPath, $"{fileName}.json");
        File.WriteAllText(fileName, jsonString);
    }

    public static void SerializeLayoutTemplate(LayoutTemplate layoutTemplate)
    {
        var jsonString = JsonConvert.SerializeObject(layoutTemplate, Formatting.Indented, new JsonSerializerSettings
        {
            TypeNameHandling = TypeNameHandling.All
        });

        var fileName = Path.Combine(TemplatesPath, $"{layoutTemplate.Name}_{layoutTemplate.Author}.json");
        File.WriteAllText(fileName, jsonString);
    }

    private static void SettingsManager_SettingValueChanged(string name, object value)
    {
        switch (name)
        {
            case "DesktopLayoutEnabled":
            {
                switch (Convert.ToBoolean(value))
                {
                    case true:
                        SetActiveLayout(desktopLayout);
                        break;
                    case false:
                        SetActiveLayout(profileLayout);
                        break;
                }
            }
                break;
        }
    }

    private static void DesktopManager_DisplayOrientationChanged(ScreenRotation rotation)
    {
        currentOrientation = rotation;

        // apply orientation
        UpdateOrientation();
    }

    private static void UpdateOrientation()
    {
        if (currentLayout is null)
            return;

        foreach (var axisLayout in currentLayout.AxisLayout)
        {
            // pull action
            var action = axisLayout.Value;

            if (action is null)
                continue;

            if (action.AutoRotate)
                action.SetOrientation(currentOrientation);
        }
    }

    private static async void SetActiveLayout(Layout layout)
    {
        while (updateLock)
            await Task.Delay(5);

        currentLayout = layout;

        // (re)apply orientation
        UpdateOrientation();
    }

    public static ControllerState MapController(ControllerState controllerState)
    {
        // when no profile active and default is disabled, do 1:1 controller mapping
        if (currentLayout is null)
            return controllerState;

        // set lock
        updateLock = true;

        // clean output state, there should be no leaking of current controller state,
        // only buttons/axes mapped from the layout should be passed on
        ControllerState outputState = new();

        foreach (var buttonState in controllerState.ButtonState.State)
        {
            var button = buttonState.Key;
            var value = buttonState.Value;

            // skip, if not mapped
            if (!currentLayout.ButtonLayout.TryGetValue(button, out var action))
                continue;

            if (action is null)
                continue;

            switch (action.ActionType)
            {
                // button to button
                case ActionType.Button:
                {
                    var bAction = action as ButtonActions;
                    value |= outputState.ButtonState[bAction.Button];

                    bAction.Execute(button, value);
                    outputState.ButtonState[bAction.Button] = bAction.GetValue();
                }
                    break;

                // button to keyboard key
                case ActionType.Keyboard:
                {
                    var kAction = action as KeyboardActions;
                    kAction.Execute(button, value);
                }
                    break;

                // button to mouse click
                case ActionType.Mouse:
                {
                    var mAction = action as MouseActions;
                    mAction.Execute(button, value);
                }
                    break;
            }
        }

        foreach (var axisLayout in currentLayout.AxisLayout)
        {
            var flags = axisLayout.Key;

            // read origin values
            var InLayout = AxisLayout.Layouts[flags];
            var InAxisX = InLayout.GetAxisFlags('X');
            var InAxisY = InLayout.GetAxisFlags('Y');

            InLayout.vector.X = controllerState.AxisState[InAxisX];
            InLayout.vector.Y = controllerState.AxisState[InAxisY];

            // pull action
            var action = axisLayout.Value;

            if (action is null)
                continue;

            switch (action.ActionType)
            {
                case ActionType.Joystick:
                {
                    var aAction = action as AxisActions;
                    aAction.Execute(InLayout);

                    // read output axis
                    var OutLayout = AxisLayout.Layouts[aAction.Axis];
                    var OutAxisX = OutLayout.GetAxisFlags('X');
                    var OutAxisY = OutLayout.GetAxisFlags('Y');

                    outputState.AxisState[OutAxisX] =
                        (short)Math.Clamp(aAction.GetValue().X, short.MinValue, short.MaxValue);
                    outputState.AxisState[OutAxisY] =
                        (short)Math.Clamp(aAction.GetValue().Y, short.MinValue, short.MaxValue);
                }
                    break;

                case ActionType.Mouse:
                {
                    var mAction = action as MouseActions;

                    // This buttonState check won't work here if UpdateInputs is event based, might need a rework in the future
                    var touched = false;
                    if (ControllerState.AxisTouchButtons.TryGetValue(InLayout.flags, out var touchButton))
                        touched = controllerState.ButtonState[touchButton];

                    mAction.Execute(InLayout, touched);
                }
                    break;

                case ActionType.Trigger:
                {
                    var tAction = action as TriggerActions;
                    tAction.Execute(InAxisY, (short)InLayout.vector.Y);

                    // read output axis
                    var OutLayout = AxisLayout.Layouts[tAction.Axis];
                    var OutAxisY = OutLayout.GetAxisFlags('Y');

                    outputState.AxisState[OutAxisY] = tAction.GetValue();
                }
                    break;
            }
        }

        // release lock
        updateLock = false;

        return outputState;
    }

    #region events

    public static event InitializedEventHandler Initialized;

    public delegate void InitializedEventHandler();

    public static event UpdatedEventHandler Updated;

    public delegate void UpdatedEventHandler(LayoutTemplate layoutTemplate);

    #endregion
}