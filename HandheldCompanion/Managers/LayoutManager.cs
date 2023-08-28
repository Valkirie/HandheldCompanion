﻿using HandheldCompanion.Actions;
using HandheldCompanion.Controllers;
using HandheldCompanion.Controls;
using HandheldCompanion.Inputs;
using HandheldCompanion.Managers.Desktop;
using HandheldCompanion.Utils;
using HandheldCompanion.Views;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Windows;

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
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite,
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

        // TODO: overwritten layout will have different GUID so it will duplicate
        layoutWatcher.Created += LayoutWatcher_Template;
        layoutWatcher.Changed += LayoutWatcher_Template;

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

    // this event is called from non main thread and it creates LayoutTemplate which is a WPF element
    private static void LayoutWatcher_Template(object sender, FileSystemEventArgs e)
    {
        // UI thread (async)
        Application.Current.Dispatcher.BeginInvoke(() =>
        {
            ProcessLayoutTemplate(e.FullPath);
        });
    }

    private static Layout? ProcessLayout(string fileName)
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
            LogManager.LogError("Could not parse Layout {0}. {1}", fileName, ex.Message);
        }

        // failed to parse
        if (layout is null)
            LogManager.LogError("Could not parse Layout {0}", fileName);

        return layout;
    }

    private static void ProcessLayoutTemplate(string fileName)
    {
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

        // todo: implement deduplication
        Templates.Add(layoutTemplate);
        Updated?.Invoke(layoutTemplate);
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
        if (CommonUtils.IsFileWritable(fileName))
            File.WriteAllText(fileName, jsonString);
    }

    public static void SerializeLayoutTemplate(LayoutTemplate layoutTemplate)
    {
        var jsonString = JsonConvert.SerializeObject(layoutTemplate, Formatting.Indented, new JsonSerializerSettings
        {
            TypeNameHandling = TypeNameHandling.All
        });

        string fileName = Path.Combine(TemplatesPath, $"{layoutTemplate.Name}_{layoutTemplate.Author}.json");
        if (CommonUtils.IsFileWritable(fileName))
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

        // TODO: this call is not from the main thread, SetActiveLayout is.
        // proper lock needed here? volatile? Interlocked.Exchange()?
        // set lock
        updateLock = true;

        // clean output state, there should be no leaking of current controller state,
        // only buttons/axes mapped from the layout should be passed on
        ControllerState outputState = new();

        // except the main gyroscope state that's not re-mappable (6 values)
        outputState.GyroState = controllerState.GyroState;

        foreach (var buttonState in controllerState.ButtonState.State)
        {
            ButtonFlags button = buttonState.Key;
            bool value = buttonState.Value;

            // skip, if not mapped
            if (!currentLayout.ButtonLayout.TryGetValue(button, out List<IActions> actions))
                continue;

            // Some long press logic. Unfortunately in case of long press actions are not 100%
            // independent of eachother. When button is pressed that has long press mapped, short
            // press should not be triggered on key down. It should only be triggered on keyup, but
            // only if released before the long timer. If timer passed, short is ignored, long is
            // pressed. Long story short :-), short press needs to be aware if long press exists.

            // TODO: change to set of ranges so several independent long presses are possible
            // if there are no long presses nothing changes
            int maxLongTime = 0;
            foreach (var action in actions)
                if (action.PressType == PressType.Long)
                    maxLongTime = Math.Max(maxLongTime, action.LongPressTime);

            foreach (var action in actions)
            {
                switch (action.ActionType)
                {
                    // button to button
                    case ActionType.Button:
                        {
                            ButtonActions bAction = action as ButtonActions;
                            bAction.Execute(button, value, maxLongTime);

                            bool outVal = bAction.GetValue() || outputState.ButtonState[bAction.Button];
                            outputState.ButtonState[bAction.Button] = outVal;
                        }
                        break;

                    // button to keyboard key
                    case ActionType.Keyboard:
                        {
                            KeyboardActions kAction = action as KeyboardActions;
                            kAction.Execute(button, value, maxLongTime);
                        }
                        break;

                    // button to mouse click
                    case ActionType.Mouse:
                        {
                            MouseActions mAction = action as MouseActions;
                            mAction.Execute(button, value, maxLongTime);
                        }
                        break;
                }
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

                        outputState.AxisState[OutAxisX] =
                            (short)Math.Clamp(outputState.AxisState[OutAxisX] + aAction.GetValue().X, short.MinValue, short.MaxValue);
                        outputState.AxisState[OutAxisY] =
                            (short)Math.Clamp(outputState.AxisState[OutAxisY] + aAction.GetValue().Y, short.MinValue, short.MaxValue);
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
            }
        }

        foreach (var axisLayout in currentLayout.GyroLayout)
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

                        outputState.AxisState[OutAxisX] =
                            (short)Math.Clamp(outputState.AxisState[OutAxisX] + aAction.GetValue().X, short.MinValue, short.MaxValue);
                        outputState.AxisState[OutAxisY] =
                            (short)Math.Clamp(outputState.AxisState[OutAxisY] + aAction.GetValue().Y, short.MinValue, short.MaxValue);
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