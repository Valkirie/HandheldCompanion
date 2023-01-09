﻿using ControllerCommon.Controllers;
using ControllerCommon.Managers;
using ControllerCommon.Utils;
using GregsStack.InputSimulatorStandard.Native;
using ModernWpf.Controls;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading;
using System.Windows;
using static HandheldCompanion.Managers.InputsHotkey;
using static HandheldCompanion.Managers.InputsManager;

namespace HandheldCompanion.Managers
{
    public static class HotkeysManager
    {
        private static string InstallPath;

        public static event HotkeyTypeCreatedEventHandler HotkeyTypeCreated;
        public delegate void HotkeyTypeCreatedEventHandler(InputsHotkeyType type);

        public static event HotkeyCreatedEventHandler HotkeyCreated;
        public delegate void HotkeyCreatedEventHandler(Hotkey hotkey);

        public static event HotkeyUpdatedEventHandler HotkeyUpdated;
        public delegate void HotkeyUpdatedEventHandler(Hotkey hotkey);

        public static event CommandExecutedEventHandler CommandExecuted;
        public delegate void CommandExecutedEventHandler(string listener);

        public static event InitializedEventHandler Initialized;
        public delegate void InitializedEventHandler();

        public static SortedDictionary<ushort, Hotkey> Hotkeys = new();
        private const short PIN_LIMIT = 9;

        private static bool IsInitialized;

        static HotkeysManager()
        {
            // initialize path
            InstallPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "HandheldCompanion", "hotkeys");
            if (!Directory.Exists(InstallPath))
                Directory.CreateDirectory(InstallPath);

            InputsManager.TriggerUpdated += TriggerUpdated;
            InputsManager.TriggerRaised += TriggerRaised;
            SettingsManager.SettingValueChanged += SettingsManager_SettingValueChanged;
        }

        public static void Start()
        {
            // process hotkeys
            foreach (var pair in InputsHotkey.InputsHotkeys)
            {
                ushort Id = pair.Key;
                InputsHotkey inputsHotkey = pair.Value;

                Hotkey hotkey = null;

                string fileName = Path.Combine(InstallPath, $"{inputsHotkey.Listener}.json");

                // check for existing hotkey
                if (File.Exists(fileName))
                    hotkey = ProcessHotkey(fileName);

                // no hotkey found or failed parsing
                if (hotkey is null)
                    hotkey = new Hotkey(Id, inputsHotkey);

                // hotkey is outdated and using an unknown inputs hotkey
                if (!InputsHotkeys.ContainsKey(hotkey.hotkeyId))
                    continue;

                // pull inputs hotkey
                hotkey.inputsHotkey = InputsHotkey.InputsHotkeys[hotkey.hotkeyId];
                switch (hotkey.inputsHotkey.hotkeyType)
                {
                    case InputsHotkeyType.UI:
                        hotkey.DrawControl(true);
                        break;
                    default:
                        hotkey.DrawControl();
                        break;
                }

                if (!Hotkeys.ContainsKey(hotkey.hotkeyId))
                    Hotkeys.Add(hotkey.hotkeyId, hotkey);
            }

            // process hotkeys types
            foreach (InputsHotkeyType type in (InputsHotkeyType[])Enum.GetValues(typeof(InputsHotkeyType)))
                HotkeyTypeCreated?.Invoke(type);

            foreach (Hotkey hotkey in Hotkeys.Values)
            {
                HotkeyCreated?.Invoke(hotkey);

                switch (hotkey.inputsHotkey.hotkeyType)
                {
                    case InputsHotkeyType.UI:
                        hotkey.inputButton.Click += (sender, e) => StartListening(hotkey, ListenerType.UI);
                        break;
                    default:
                        hotkey.inputButton.Click += (sender, e) => StartListening(hotkey, ListenerType.Default);
                        break;
                }

                hotkey.outputButton.Click += (sender, e) => StartListening(hotkey, ListenerType.Output);
                hotkey.pinButton.Click += (sender, e) => PinOrUnpinHotkey(hotkey);

                hotkey.quickButton.PreviewTouchDown += (sender, e) => { InputsManager.InvokeTrigger(hotkey, true, false); };
                hotkey.quickButton.PreviewMouseDown += (sender, e) => { InputsManager.InvokeTrigger(hotkey, true, false); };
                hotkey.quickButton.PreviewMouseUp += (sender, e) => { InputsManager.InvokeTrigger(hotkey, false, true); };

                hotkey.Updated += (hotkey) => SerializeHotkey(hotkey, true);
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
            var hotkey = Hotkeys.Values.Where(item => item.inputsHotkey.Listener.Contains(name)).FirstOrDefault();
            if (hotkey is null)
                return;

            Application.Current.Dispatcher.Invoke(new Action(() =>
            {
                switch (name)
                {
                    case "SteamDeckLizardMouse":
                    case "SteamDeckLizardButtons":
                        {
                            bool toggle = Convert.ToBoolean(value);
                            hotkey.SetToggle(toggle);
                        }
                        break;
                }
            }));
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
                            _ = Dialog.ShowAsync($"{Properties.Resources.SettingsPage_UpdateWarning}",
                                $"You can't pin more than {PIN_LIMIT} hotkeys",
                                ContentDialogButton.Primary, string.Empty, $"{Properties.Resources.ProfilesPage_OK}");

                            return;
                        }

                        hotkey.StartPinning();
                    }
                    break;
                case true:
                    {
                        hotkey.StopPinning();
                    }
                    break;
            }

            // overwrite current file
            SerializeHotkey(hotkey, true);
        }

        private static int CountPinned()
        {
            return Hotkeys.Values.Where(item => item.IsPinned).Count();
        }

        private static void TriggerUpdated(string listener, InputsChord inputs, ListenerType type)
        {
            Application.Current.Dispatcher.Invoke(new Action(() =>
            {
                // we use @ as a special character to link two ore more listeners together
                listener = listener.TrimEnd('@');

                var hotkeys = Hotkeys.Values.Where(item => item.inputsHotkey.Listener.Contains(listener));
                foreach (Hotkey hotkey in hotkeys)
                {
                    hotkey.StopListening(inputs, type);

                    // overwrite current file
                    SerializeHotkey(hotkey, true);
                }
            }));
        }

        private static Hotkey ProcessHotkey(string fileName)
        {
            Hotkey hotkey = null;
            try
            {
                string outputraw = File.ReadAllText(fileName);
                hotkey = JsonSerializer.Deserialize<Hotkey>(outputraw);
            }
            catch (Exception ex)
            {
                LogManager.LogError("Could not parse hotkey {0}. {1}", fileName, ex.Message);
            }

            return hotkey;
        }

        public static void SerializeHotkey(Hotkey hotkey, bool overwrite = false)
        {
            string listener = hotkey.inputsHotkey.Listener;

            string settingsPath = Path.Combine(InstallPath, $"{listener}.json");
            if (!File.Exists(settingsPath) || overwrite)
            {
                var options = new JsonSerializerOptions { WriteIndented = true };
                string jsonString = JsonSerializer.Serialize(hotkey, options);
                File.WriteAllText(settingsPath, jsonString);
            }

            // raise event
            HotkeyUpdated?.Invoke(hotkey);
        }

        public static void TriggerRaised(string listener, InputsChord input, bool IsKeyDown, bool IsKeyUp)
        {
            // we use @ as a special character to link two ore more listeners together
            listener = listener.TrimEnd('@');

            var hotkeys = Hotkeys.Values.Where(item => item.inputsHotkey.Listener.Contains(listener));

            foreach (Hotkey hotkey in hotkeys)
            {
                Application.Current.Dispatcher.Invoke(new Action(() =>
                {
                    hotkey.Highlight();
                }));

                // These are special shortcut keys with no related events
                if (hotkey == hotkeys.Last() && hotkey.inputsHotkey.hotkeyType == InputsHotkeyType.UI)
                    return;
            }

            ProcessEx fProcess = ProcessManager.GetForegroundProcess();

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
                        InputsManager.KeyPress(new VirtualKeyCode[] { VirtualKeyCode.LWIN, VirtualKeyCode.VK_D });
                        break;
                    case "shortcutESC":
                        if (fProcess is not null && fProcess.Filter == ProcessEx.ProcessFilter.Allowed)
                        {
                            ProcessUtils.SetForegroundWindow(fProcess.MainWindowHandle);
                            InputsManager.KeyPress(VirtualKeyCode.ESCAPE);
                        }
                        break;
                    case "shortcutExpand":
                        if (fProcess is not null && fProcess.Filter == ProcessEx.ProcessFilter.Allowed)
                        {
                            var Placement = ProcessUtils.GetPlacement(fProcess.MainWindowHandle);

                            switch (Placement.showCmd)
                            {
                                case ProcessUtils.ShowWindowCommands.Normal:
                                case ProcessUtils.ShowWindowCommands.Minimized:
                                    ProcessUtils.ShowWindow(fProcess.MainWindowHandle, (int)ProcessUtils.ShowWindowCommands.Maximized);
                                    break;
                                case ProcessUtils.ShowWindowCommands.Maximized:
                                    ProcessUtils.ShowWindow(fProcess.MainWindowHandle, (int)ProcessUtils.ShowWindowCommands.Restored);
                                    break;
                            }
                        }
                        break;
                    case "shortcutTaskview":
                        InputsManager.KeyPress(new VirtualKeyCode[] { VirtualKeyCode.LWIN, VirtualKeyCode.TAB });
                        break;
                    case "shortcutTaskManager":
                        InputsManager.KeyPress(new VirtualKeyCode[] { VirtualKeyCode.LCONTROL, VirtualKeyCode.LSHIFT, VirtualKeyCode.ESCAPE });
                        break;
                    case "shortcutGuide":
                        // temporary, move me to remapper !
                        ControllerManager.buttonMaps.Clear();
                        ControllerManager.buttonMaps[input.GamepadButtons] = ControllerButtonFlags.Special;
                        break;
                    case "suspendResumeTask":
                        {
                            var sProcess = ProcessManager.GetSuspendedProcess();

                            if (sProcess is null || sProcess.Filter != ProcessEx.ProcessFilter.Allowed)
                                break;

                            if (sProcess.IsSuspended())
                                ProcessManager.ResumeProcess(sProcess);
                            else
                                ProcessManager.SuspendProcess(fProcess);
                        }
                        break;
                    case "shortcutKillApp":
                        if (fProcess is not null)
                        {
                            fProcess.Process.Kill();
                        }
                        break;

                    case "SteamDeckLizardMouse":
                    case "SteamDeckLizardButtons":
                        {
                            bool SteamDeckLizardMode = SettingsManager.GetBoolean(listener);
                            SettingsManager.SetProperty(listener, !SteamDeckLizardMode);
                        }
                        break;

                    default:
                        InputsManager.KeyPress(input.OutputKeys);
                        break;
                }

                LogManager.LogDebug("Executed Hotkey: {0}", listener);

                // play a tune to notify a command was executed
                DesktopManager.PlayWindowsMedia("Windows Navigation Start.wav");

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
            switch (hotkey.inputsHotkey.Listener)
            {
                case "shortcutGuide":
                    ControllerManager.buttonMaps.Remove(hotkey.inputsChord.GamepadButtons);
                    break;
            }
        }
    }
}
