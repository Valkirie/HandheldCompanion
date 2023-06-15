using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using Windows.System;
using ControllerCommon.Controllers;
using ControllerCommon.Managers;
using ControllerCommon.Utils;
using GregsStack.InputSimulatorStandard.Native;
using HandheldCompanion.Controls;
using HandheldCompanion.Properties;
using HandheldCompanion.Simulators;
using HandheldCompanion.Views;
using Inkore.UI.WPF.Modern.Controls;
using Newtonsoft.Json;
using static HandheldCompanion.Managers.InputsHotkey;
using static HandheldCompanion.Managers.InputsManager;

namespace HandheldCompanion.Managers;

public static class HotkeysManager
{
    public delegate void CommandExecutedEventHandler(string listener);

    public delegate void HotkeyCreatedEventHandler(Hotkey hotkey);

    public delegate void HotkeyTypeCreatedEventHandler(InputsHotkeyType type);

    public delegate void HotkeyUpdatedEventHandler(Hotkey hotkey);

    public delegate void InitializedEventHandler();

    private const short PIN_LIMIT = 18;
    private static readonly string InstallPath;

    public static SortedDictionary<ushort, Hotkey> Hotkeys = new();

    private static bool IsInitialized;

    static HotkeysManager()
    {
        // initialize path
        InstallPath = Path.Combine(MainWindow.SettingsPath, "hotkeys");
        if (!Directory.Exists(InstallPath))
            Directory.CreateDirectory(InstallPath);

        InputsManager.TriggerUpdated += TriggerUpdated;
        InputsManager.TriggerRaised += TriggerRaised;
        SettingsManager.SettingValueChanged += SettingsManager_SettingValueChanged;
        ControllerManager.ControllerSelected += ControllerManager_ControllerSelected;
    }

    public static event HotkeyTypeCreatedEventHandler HotkeyTypeCreated;

    public static event HotkeyCreatedEventHandler HotkeyCreated;

    public static event HotkeyUpdatedEventHandler HotkeyUpdated;

    public static event CommandExecutedEventHandler CommandExecuted;

    public static event InitializedEventHandler Initialized;

    private static void ControllerManager_ControllerSelected(IController Controller)
    {
        foreach (var hotkey in Hotkeys.Values)
            hotkey.ControllerSelected(Controller);
    }

    public static void Start()
    {
        // process hotkeys types
        foreach (var type in (InputsHotkeyType[])Enum.GetValues(typeof(InputsHotkeyType)))
            HotkeyTypeCreated?.Invoke(type);

        // process hotkeys
        foreach (var pair in InputsHotkeys)
        {
            var Id = pair.Key;
            var inputsHotkey = pair.Value;

            Hotkey hotkey = null;

            var fileName = Path.Combine(InstallPath, $"{inputsHotkey.Listener}.json");

            // check for existing hotkey
            if (File.Exists(fileName))
                hotkey = ProcessHotkey(fileName);

            // no hotkey found or failed parsing
            if (hotkey is null)
            {
                hotkey = new Hotkey(Id);
                hotkey.IsPinned = inputsHotkey.DefaultPinned;
            }

            // hotkey is outdated and using an unknown inputs hotkey
            if (!InputsHotkeys.TryGetValue(hotkey.hotkeyId, out var foundHotkey))
                continue;

            // pull inputs hotkey
            hotkey.SetInputsHotkey(foundHotkey);
            hotkey.Draw();

            if (!Hotkeys.ContainsKey(hotkey.hotkeyId))
                Hotkeys.Add(hotkey.hotkeyId, hotkey);
        }

        foreach (var hotkey in Hotkeys.Values)
        {
            hotkey.Listening += StartListening;
            hotkey.Pinning += PinOrUnpinHotkey;
            hotkey.Summoned += hotkey => InvokeTrigger(hotkey, false, true);
            hotkey.Updated += hotkey => SerializeHotkey(hotkey, true);

            if (!string.IsNullOrEmpty(hotkey.inputsHotkey.Settings))
                hotkey.IsEnabled = SettingsManager.GetBoolean(hotkey.inputsHotkey.Settings);

            HotkeyCreated?.Invoke(hotkey);
        }

        IsInitialized = true;
        Initialized?.Invoke();

        LogManager.LogInformation("{0} has started", "HotkeysManager");
    }

    public static void Stop()
    {
        if (!IsInitialized)
            return;

        IsInitialized = false;

        LogManager.LogInformation("{0} has stopped", "HotkeysManager");
    }

    private static void SettingsManager_SettingValueChanged(string name, object value)
    {
        // UI thread (async)
        Application.Current.Dispatcher.BeginInvoke(() =>
        {
            // manage toggle type hotkeys
            foreach (var hotkey in Hotkeys.Values.Where(item => item.inputsHotkey.Listener.Equals(name)))
            {
                if (!hotkey.inputsHotkey.IsToggle)
                    continue;

                var toggle = Convert.ToBoolean(value);
                hotkey.SetToggle(toggle);
            }

            // manage settings type hotkeys
            foreach (var hotkey in Hotkeys.Values.Where(item => item.inputsHotkey.Settings.Contains(name)))
            {
                var enabled = SettingsManager.GetBoolean(hotkey.inputsHotkey.Settings);
                hotkey.IsEnabled = enabled;
            }
        });
    }

    private static void StartListening(Hotkey hotkey, ListenerType type)
    {
        InputsManager.StartListening(hotkey, type);
        hotkey.StartListening(type);
    }

    private static void PinOrUnpinHotkey(Hotkey hotkey)
    {
        switch (hotkey.IsPinned)
        {
            case false:
            {
                var count = CountPinned();

                if (count >= PIN_LIMIT)
                {
                    _ = Dialog.ShowAsync($"{Resources.SettingsPage_UpdateWarning}",
                        $"You can't pin more than {PIN_LIMIT} hotkeys",
                        ContentDialogButton.Primary, string.Empty, $"{Resources.ProfilesPage_OK}");

                    return;
                }

                hotkey.IsPinned = true;
            }
                break;
            case true:
                hotkey.IsPinned = false;
                break;
        }

        // overwrite current file
        SerializeHotkey(hotkey, true);
    }

    private static int CountPinned()
    {
        return Hotkeys.Values.Count(item => item.IsPinned);
    }

    private static void TriggerUpdated(string listener, InputsChord inputs, ListenerType type)
    {
        // UI thread (async)
        Application.Current.Dispatcher.BeginInvoke(() =>
        {
            // we use @ as a special character to link two ore more listeners together
            listener = listener.TrimEnd('@');

            var hotkeys = Hotkeys.Values.Where(item => item.inputsHotkey.Listener.Contains(listener));
            foreach (var hotkey in hotkeys)
            {
                hotkey.StopListening(inputs, type);

                // overwrite current file
                SerializeHotkey(hotkey, true);
            }
        });
    }

    private static Hotkey ProcessHotkey(string fileName)
    {
        Hotkey hotkey = null;
        try
        {
            var outputraw = File.ReadAllText(fileName);
            hotkey = JsonConvert.DeserializeObject<Hotkey>(outputraw);
        }
        catch (Exception ex)
        {
            LogManager.LogError("Could not parse hotkey {0}. {1}", fileName, ex.Message);
        }

        return hotkey;
    }

    public static void SerializeHotkey(Hotkey hotkey, bool overwrite = false)
    {
        var listener = hotkey.inputsHotkey.Listener;

        var settingsPath = Path.Combine(InstallPath, $"{listener}.json");
        if (!File.Exists(settingsPath) || overwrite)
        {
            var jsonString = JsonConvert.SerializeObject(hotkey, Formatting.Indented);
            File.WriteAllText(settingsPath, jsonString);
        }

        // raise event
        HotkeyUpdated?.Invoke(hotkey);
    }

    public static void TriggerRaised(string listener, InputsChord input, InputsHotkeyType type, bool IsKeyDown,
        bool IsKeyUp)
    {
        // UI thread (async)
        Application.Current.Dispatcher.BeginInvoke(() =>
        {
            // we use @ as a special character to link two ore more listeners together
            var trimmed = listener.TrimEnd('@');
            var hotkeys = Hotkeys.Values.Where(item => item.inputsHotkey.Listener.Contains(trimmed));
            foreach (var hotkey in hotkeys)
                hotkey.Highlight();
        });

        // These are special shortcut keys with no related events
        switch (type)
        {
            case InputsHotkeyType.Embedded:
                return;
        }

        var fProcess = ProcessManager.GetForegroundProcess();

        try
        {
            switch (listener)
            {
                case "shortcutKeyboard":
                    new Thread(() =>
                    {
                        var uiHostNoLaunch = new ProcessUtils.UIHostNoLaunch();
                        var tipInvocation = (ProcessUtils.ITipInvocation)uiHostNoLaunch;
                        tipInvocation.Toggle(ProcessUtils.GetDesktopWindow());
                        Marshal.ReleaseComObject(uiHostNoLaunch);
                    }).Start();
                    break;
                case "shortcutDesktop":
                    KeyboardSimulator.KeyPress(new[] { VirtualKeyCode.LWIN, VirtualKeyCode.VK_D });
                    break;
                case "shortcutESC":
                    if (fProcess is not null)
                    {
                        ProcessUtils.SetForegroundWindow(fProcess.MainWindowHandle);
                        KeyboardSimulator.KeyPress(VirtualKeyCode.ESCAPE);
                    }

                    break;
                case "shortcutExpand":
                    if (fProcess is not null)
                    {
                        var Placement = ProcessUtils.GetPlacement(fProcess.MainWindowHandle);

                        switch (Placement.showCmd)
                        {
                            case ProcessUtils.ShowWindowCommands.Normal:
                            case ProcessUtils.ShowWindowCommands.Minimized:
                                ProcessUtils.ShowWindow(fProcess.MainWindowHandle,
                                    (int)ProcessUtils.ShowWindowCommands.Maximized);
                                break;
                            case ProcessUtils.ShowWindowCommands.Maximized:
                                ProcessUtils.ShowWindow(fProcess.MainWindowHandle,
                                    (int)ProcessUtils.ShowWindowCommands.Restored);
                                break;
                        }
                    }

                    break;
                case "shortcutTaskview":
                    KeyboardSimulator.KeyPress(new[] { VirtualKeyCode.LWIN, VirtualKeyCode.TAB });
                    break;
                case "shortcutTaskManager":
                    KeyboardSimulator.KeyPress(new[]
                        { VirtualKeyCode.LCONTROL, VirtualKeyCode.LSHIFT, VirtualKeyCode.ESCAPE });
                    break;
                case "shortcutActionCenter":
                {
                    var uri = new Uri("ms-actioncenter");
                    var success = Launcher.LaunchUriAsync(uri);
                }
                    break;
                case "shortcutControlCenter":
                {
                    var uri = new Uri(
                        "ms-actioncenter:controlcenter/&suppressAnimations=false&showFooter=true&allowPageNavigation=true");
                    var success = Launcher.LaunchUriAsync(uri);
                }
                    break;
                case "shortcutPrintScreen":
                    KeyboardSimulator.KeyPress(
                        new[] { VirtualKeyCode.LWIN, VirtualKeyCode.LSHIFT, VirtualKeyCode.VK_S });
                    break;
                case "suspendResumeTask":
                {
                    var sProcess = ProcessManager.GetLastSuspendedProcess();

                    if (sProcess is null || sProcess.Filter != ProcessEx.ProcessFilter.Allowed)
                        break;

                    if (sProcess.IsSuspended())
                        ProcessManager.ResumeProcess(sProcess);
                    else
                        ProcessManager.SuspendProcess(fProcess);
                }
                    break;
                case "shortcutKillApp":
                    if (fProcess is not null) fProcess.Process.Kill();
                    break;
                case "QuietModeToggled":
                {
                    var value = !SettingsManager.GetBoolean(listener);
                    SettingsManager.SetProperty(listener, value);

                    ToastManager.SendToast("Quiet mode", $"is now {(value ? "enabled" : "disabled")}");
                }
                    break;

                case "OnScreenDisplayLevel":
                {
                    var value = !SettingsManager.GetBoolean(listener);
                    SettingsManager.SetProperty(listener, value);
                }
                    break;

                // temporary settings
                case "DesktopLayoutEnabled":
                {
                    var value = !SettingsManager.GetBoolean(listener, true);
                    SettingsManager.SetProperty(listener, value, false, true);

                    ToastManager.SendToast("Desktop layout", $"is now {(value ? "enabled" : "disabled")}");
                }
                    break;

                default:
                    KeyboardSimulator.KeyPress(input.OutputKeys.ToArray());
                    break;
            }

            LogManager.LogDebug("Executed Hotkey: {0}", listener);

            // play a tune to notify a command was executed
            SystemManager.PlayWindowsMedia("Windows Navigation Start.wav");

            // raise an event
            CommandExecuted?.Invoke(listener);
        }
        catch (Exception ex)
        {
            LogManager.LogError("Failed to parse trigger {0}, {1}", listener, ex.Message);
        }
    }

    internal static void ClearHotkey(Hotkey hotkey)
    {
        // do something
    }
}