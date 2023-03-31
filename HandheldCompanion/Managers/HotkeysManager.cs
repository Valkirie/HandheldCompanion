using ControllerCommon.Controllers;
using ControllerCommon.Managers;
using ControllerCommon.Utils;
using GregsStack.InputSimulatorStandard.Native;
using HandheldCompanion.Controls;
using HandheldCompanion.Simulators;
using ModernWpf.Controls;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
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
            ControllerManager.ControllerSelected += ControllerManager_ControllerSelected;
        }

        private static void ControllerManager_ControllerSelected(IController Controller)
        {
            foreach (Hotkey hotkey in Hotkeys.Values)
                hotkey.ControllerSelected(Controller);
        }

        public static void Start()
        {
            // process hotkeys types
            foreach (InputsHotkeyType type in (InputsHotkeyType[])Enum.GetValues(typeof(InputsHotkeyType)))
                HotkeyTypeCreated?.Invoke(type);

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
                    hotkey = new Hotkey(Id);

                // hotkey is outdated and using an unknown inputs hotkey
                if (!InputsHotkeys.ContainsKey(hotkey.hotkeyId))
                    continue;

                // pull inputs hotkey
                hotkey.SetInputsHotkey(InputsHotkeys[hotkey.hotkeyId]);
                hotkey.Refresh();

                if (!Hotkeys.ContainsKey(hotkey.hotkeyId))
                    Hotkeys.Add(hotkey.hotkeyId, hotkey);
            }

            foreach (Hotkey hotkey in Hotkeys.Values)
            {
                hotkey.Listening += StartListening;
                hotkey.Pinning += PinOrUnpinHotkey;
                hotkey.Summoned += (hotkey) => InvokeTrigger(hotkey, false, true);
                hotkey.Updated += (hotkey) => SerializeHotkey(hotkey, true);

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
            // UI thread
            Application.Current.Dispatcher.BeginInvoke(() =>
            {
                switch (name)
                {
                    case "SteamDeckLizardMouse":
                    case "SteamDeckLizardButtons":
                    case "shortcutDesktopLayout":
                    case "QuietModeToggled":
                        {
                            var hotkey = Hotkeys.Values.Where(item => item.inputsHotkey.Listener.Contains(name)).FirstOrDefault();
                            if (hotkey is null)
                                return;

                            bool toggle = Convert.ToBoolean(value);
                            hotkey.SetToggle(toggle);
                        }
                        break;

                    case "QuietModeEnabled":
                        {
                            var hotkey = Hotkeys.Values.Where(item => item.inputsHotkey.Settings.Contains(name)).FirstOrDefault();
                            if (hotkey is null)
                                return;

                            bool toggle = Convert.ToBoolean(value);
                            hotkey.IsEnabled = toggle;
                        }
                        break;
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
                            _ = Dialog.ShowAsync($"{Properties.Resources.SettingsPage_UpdateWarning}",
                                $"You can't pin more than {PIN_LIMIT} hotkeys",
                                ContentDialogButton.Primary, string.Empty, $"{Properties.Resources.ProfilesPage_OK}");

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
            return Hotkeys.Values.Where(item => item.IsPinned).Count();
        }

        private static void TriggerUpdated(string listener, InputsChord inputs, ListenerType type)
        {
            // UI thread
            Application.Current.Dispatcher.Invoke(() =>
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
            });
        }

        private static Hotkey ProcessHotkey(string fileName)
        {
            Hotkey hotkey = null;
            try
            {
                string outputraw = File.ReadAllText(fileName);
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
            string listener = hotkey.inputsHotkey.Listener;

            string settingsPath = Path.Combine(InstallPath, $"{listener}.json");
            if (!File.Exists(settingsPath) || overwrite)
            {
                string jsonString = JsonConvert.SerializeObject(hotkey, Formatting.Indented);
                File.WriteAllText(settingsPath, jsonString);
            }

            // raise event
            HotkeyUpdated?.Invoke(hotkey);
        }

        public static void TriggerRaised(string listener, InputsChord input, bool IsKeyDown, bool IsKeyUp)
        {
            // we use @ as a special character to link two ore more listeners together
            listener = listener.TrimEnd('@');

            var hotkey = Hotkeys.Values.Where(item => item.inputsHotkey.Listener.Contains(listener)).FirstOrDefault();
            if (hotkey is null)
                return;

            // Hotkey is disabled
            if (!hotkey.IsEnabled)
                return;

            // These are special shortcut keys with no related events
            if (hotkey.inputsHotkey.hotkeyType == InputsHotkeyType.Embedded)
                return;

            var hotkeys = Hotkeys.Values.Where(item => item.inputsHotkey.Listener.Contains(listener));

            // UI thread
            Application.Current.Dispatcher.Invoke(() =>
            {
                foreach (var htkey in hotkeys)
                    hotkey.Highlight();
            });

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
                        KeyboardSimulator.KeyPress(new VirtualKeyCode[] { VirtualKeyCode.LWIN, VirtualKeyCode.VK_D });
                        break;
                    case "shortcutESC":
                        if (fProcess is not null && fProcess.Filter == ProcessEx.ProcessFilter.Allowed)
                        {
                            ProcessUtils.SetForegroundWindow(fProcess.MainWindowHandle);
                            KeyboardSimulator.KeyPress(VirtualKeyCode.ESCAPE);
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
                        KeyboardSimulator.KeyPress(new VirtualKeyCode[] { VirtualKeyCode.LWIN, VirtualKeyCode.TAB });
                        break;
                    case "shortcutTaskManager":
                        KeyboardSimulator.KeyPress(new VirtualKeyCode[] { VirtualKeyCode.LCONTROL, VirtualKeyCode.LSHIFT, VirtualKeyCode.ESCAPE });
                        break;
                    case "shortcutActionCenter":
                        {
                            var uri = new Uri("ms-actioncenter");
                            var success = Windows.System.Launcher.LaunchUriAsync(uri);
                        }
                        break;
                    case "shortcutControlCenter":
                        {
                            var uri = new Uri("ms-actioncenter:controlcenter/&suppressAnimations=false&showFooter=true&allowPageNavigation=true");
                            var success = Windows.System.Launcher.LaunchUriAsync(uri);
                        }
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
                    case "QuietModeToggled":
                        {
                            bool value = !SettingsManager.GetBoolean(listener);
                            SettingsManager.SetProperty(listener, value);

                            ToastManager.SendToast("Quiet mode", $"is now {(value ? "enabled" : "disabled")}");
                        }
                        break;

                    // temporary settings
                    case "shortcutDesktopLayout":
                        {
                            bool value = !SettingsManager.GetBoolean(listener, true);
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
}
