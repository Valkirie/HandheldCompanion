using ControllerCommon;
using ControllerCommon.Controllers;
using ControllerCommon.Managers;
using ControllerCommon.Utils;
using GregsStack.InputSimulatorStandard.Native;
using HandheldCompanion.Managers.Classes;
using HandheldCompanion.Views;
using ModernWpf.Controls;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading;
using System.Windows;
using static HandheldCompanion.Managers.Classes.InputsHotkey;

namespace HandheldCompanion.Managers
{
    public static class HotkeysManager
    {
        static string Path;

        public static event HotkeyTypeCreatedEventHandler HotkeyTypeCreated;
        public delegate void HotkeyTypeCreatedEventHandler(InputsHotkeyType type);

        public static event HotkeyCreatedEventHandler HotkeyCreated;
        public delegate void HotkeyCreatedEventHandler(Hotkey hotkey);

        public static event HotkeyUpdatedEventHandler HotkeyUpdated;
        public delegate void HotkeyUpdatedEventHandler(Hotkey hotkey);

        public static event CommandExecutedEventHandler CommandExecuted;
        public delegate void CommandExecutedEventHandler(string listener);

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
        }

        public static void Stop()
        {
            if (!IsInitialized)
                return;
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
            var fProcess = ProcessManager.GetForegroundProcess();

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
                        InputsManager.KeyPress(new VirtualKeyCode[] { VirtualKeyCode.LWIN, VirtualKeyCode.VK_D });
                        break;
                    case "shortcutESC":
                        if (fProcess != null)
                        {
                            ProcessUtils.SetForegroundWindow(fProcess.MainWindowHandle);
                            InputsManager.KeyPress(VirtualKeyCode.ESCAPE);
                        }
                        break;
                    case "shortcutExpand":
                        if (fProcess != null)
                        {
                            ProcessUtils.SetForegroundWindow(fProcess.MainWindowHandle);
                            InputsManager.KeyStroke(VirtualKeyCode.LMENU, VirtualKeyCode.RETURN);
                        }
                        break;
                    case "shortcutTaskview":
                        InputsManager.KeyPress(new VirtualKeyCode[] { VirtualKeyCode.LWIN, VirtualKeyCode.TAB });
                        break;
                    case "shortcutTaskManager":
                        InputsManager.KeyPress(new VirtualKeyCode[] { VirtualKeyCode.LCONTROL, VirtualKeyCode.LSHIFT, VirtualKeyCode.ESCAPE });
                        break;
                    case "shortcutGuide":
                        // comments: should be handled by a mapper, not hotkeys
                        var controller = ControllerManager.GetTargetController();
                        controller?.InjectButton(ControllerButtonFlags.Special, IsKeyDown, IsKeyUp);
                        break;
                    case "suspendResumeTask":
                        {
                            var sProcess = ProcessManager.GetSuspendedProcess();

                            if (sProcess != null)
                                ProcessManager.ResumeProcess(sProcess);
                            else if (!fProcess.IsIgnored)
                                ProcessManager.SuspendProcess(fProcess);
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
    }
}
