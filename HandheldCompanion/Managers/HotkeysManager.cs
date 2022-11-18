using ControllerCommon.Controllers;
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
using Shell
/* Unmerged change from project 'HandheldCompanion (net6.0-windows10.0.19041.0)'
Before:
using Shell32;
using Shell = Shell32.Shell;
After:
using Shell = Shell32.Shell;
*/
 = Shell32.Shell;

namespace HandheldCompanion.Managers
{
    public static class HotkeysManager
    {
        private static string Path;
        private static Shell Shell = new Shell();

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
            Path = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "HandheldCompanion", "hotkeys");
            if (!Directory.Exists(Path))
                Directory.CreateDirectory(Path);

            InputsManager.TriggerUpdated += TriggerUpdated;
            InputsManager.TriggerRaised += TriggerRaised;
        }

        public static void Start()
        {
            // process hotkeys
            foreach (var pair in InputsHotkey.InputsHotkeys)
            {
                ushort Id = pair.Key;
                InputsHotkey inputsHotkey = pair.Value;

                Hotkey hotkey = null;

                string fileName = System.IO.Path.Combine(Path, $"{inputsHotkey.Listener}.json");

                if (File.Exists(fileName))
                    hotkey = ProcessHotkey(fileName);

                if (hotkey is null)
                    hotkey = new Hotkey(Id, inputsHotkey);

                hotkey.inputsHotkey = InputsHotkey.InputsHotkeys[hotkey.hotkeyId];
                hotkey.DrawControl();

                Hotkeys.Add(hotkey.hotkeyId, hotkey);
            }

            // process hotkeys types
            foreach (InputsHotkeyType type in (InputsHotkeyType[])Enum.GetValues(typeof(InputsHotkeyType)))
                HotkeyTypeCreated?.Invoke(type);

            foreach (Hotkey hotkey in Hotkeys.Values)
            {
                HotkeyCreated?.Invoke(hotkey);

                hotkey.inputButton.Click += (sender, e) => StartListening(hotkey, false);
                hotkey.outputButton.Click += (sender, e) => StartListening(hotkey, true);
                hotkey.pinButton.Click += (sender, e) => PinOrUnpinHotkey(hotkey);
                hotkey.quickButton.PreviewTouchDown += (sender, e) => { InputsManager.InvokeTrigger(hotkey, true, false); };
                hotkey.quickButton.PreviewMouseDown += (sender, e) => { InputsManager.InvokeTrigger(hotkey, true, false); };
                hotkey.quickButton.PreviewMouseUp += (sender, e) => { InputsManager.InvokeTrigger(hotkey, false, true); };
            }

            IsInitialized = true;
            Initialized?.Invoke();
        }

        public static void Stop()
        {
            if (!IsInitialized)
                return;

            IsInitialized = false;
        }

        private static void StartListening(Hotkey hotkey, bool IsCombo)
        {
            InputsManager.StartListening(hotkey, IsCombo);
            hotkey.StartListening(IsCombo);
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
            HotkeyUpdated?.Invoke(hotkey);
        }

        private static int CountPinned()
        {
            return Hotkeys.Values.Where(item => item.IsPinned).Count();
        }

        private static void TriggerUpdated(string listener, InputsChord inputs, bool IsCombo)
        {
            Application.Current.Dispatcher.Invoke(new Action(() =>
            {
                Hotkey hotkey = Hotkeys.Values.Where(item => item.inputsHotkey.Listener.Equals(listener)).FirstOrDefault();

                if (hotkey is null)
                    return;

                hotkey.StopListening(inputs, IsCombo);

                // overwrite current file
                SerializeHotkey(hotkey, true);
                HotkeyUpdated?.Invoke(hotkey);
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

            // failed to parse
            if (hotkey == null)
            {
                LogManager.LogError("Error while parsing hotkey {0}. Object is null.", fileName);
            }

            if (!InputsHotkey.InputsHotkeys.ContainsKey(hotkey.hotkeyId))
            {
                LogManager.LogError("Error while parsing {0}. InputsHotkey is outdated.", fileName);
            }

            return hotkey;
        }

        public static void SerializeHotkey(Hotkey hotkey, bool overwrite = false)
        {
            string listener = hotkey.inputsHotkey.Listener;

            string settingsPath = System.IO.Path.Combine(Path, $"{listener}.json");
            if (!File.Exists(settingsPath) || overwrite)
            {
                var options = new JsonSerializerOptions { WriteIndented = true };
                string jsonString = JsonSerializer.Serialize(hotkey, options);
                File.WriteAllText(settingsPath, jsonString);
            }
        }

        public static void TriggerRaised(string listener, InputsChord input, bool IsKeyDown, bool IsKeyUp)
        {
            ProcessEx fProcess = ProcessManager.GetForegroundProcess();

            Application.Current.Dispatcher.Invoke(new Action(() =>
            {
                Hotkey hotkey = Hotkeys.Values.Where(item => item.inputsHotkey.Listener.Equals(listener)).FirstOrDefault();

                if (hotkey is null)
                    return;

                hotkey.Highlight();
            }));

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
                        Shell.ToggleDesktop();
                        break;
                    case "shortcutESC":
                        if (fProcess != null && !fProcess.IsIgnored)
                        {
                            ProcessUtils.SetForegroundWindow(fProcess.MainWindowHandle);
                            InputsManager.KeyPress(VirtualKeyCode.ESCAPE);
                        }
                        break;
                    case "shortcutExpand":
                        if (fProcess != null && !fProcess.IsIgnored)
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
                        ControllerManager.buttonMaps[input.GamepadButtons] = ControllerButtonFlags.Special;
                        break;
                    case "suspendResumeTask":
                        {
                            var sProcess = ProcessManager.GetSuspendedProcess();

                            if (sProcess is null || sProcess.IsIgnored)
                                break;

                            if (sProcess.IsSuspended())
                                ProcessManager.ResumeProcess(sProcess);
                            else
                                ProcessManager.SuspendProcess(fProcess);
                        }
                        break;
                    case "shortcutKillApp":
                        if (fProcess != null)
                        {
                            fProcess.Process.Kill();
                        }
                        break;
                    default:
                        InputsManager.KeyPress(input.OutputKeys);
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
            switch (hotkey.inputsHotkey.Listener)
            {
                case "shortcutGuide":
                    ControllerManager.buttonMaps[hotkey.inputsChord.GamepadButtons] = ControllerButtonFlags.None;
                    break;
            }
        }
    }
}
