using ControllerCommon;
using ControllerCommon.Managers;
using ControllerCommon.Utils;
using GregsStack.InputSimulatorStandard.Native;
using HandheldCompanion.Managers.Classes;
using HandheldCompanion.Views;
using System;
using System.Collections.Generic;
using System.IO;
using System.Media;
using System.Runtime.InteropServices;
using System.Text.Json;
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

        public static event CommandExecutedEventHandler CommandExecuted;
        public delegate void CommandExecutedEventHandler(string listener);

        public static Dictionary<string, Hotkey> Hotkeys = new();

        static HotkeysManager()
        {
            // initialize path
            Path = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "HandheldCompanion", "hotkeys");
            if (!Directory.Exists(Path))
                Directory.CreateDirectory(Path);

            foreach (var pair in InputsHotkey.Hotkeys)
            {
                ushort Id = pair.Key;
                InputsHotkey inputsHotkey = pair.Value;

                Hotkey hotkey = new Hotkey(Id, inputsHotkey);
                SerializeHotkey(hotkey);
            }

            InputsManager.TriggerUpdated += TriggerUpdated;
            InputsManager.TriggerRaised += TriggerRaised;
        }

        public static void Start()
        {
            // process existing hotkeys types
            foreach (InputsHotkeyType type in (InputsHotkeyType[])Enum.GetValues(typeof(InputsHotkeyType)))
                HotkeyTypeCreated?.Invoke(type);

            // process existing hotkeys
            string[] fileEntries = Directory.GetFiles(Path, "*.json", SearchOption.AllDirectories);
            foreach (string fileName in fileEntries)
                ProcessHotkey(fileName);
        }

        private static void ProcessHotkey(string fileName)
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
            if (hotkey == null || hotkey.hotkeyId == 0 || hotkey.inputsChord == null)
            {
                LogManager.LogError("Could not parse hotkey {0}.", fileName);
                return;
            }

            hotkey.inputsHotkey = InputsHotkey.Hotkeys[hotkey.hotkeyId];
            hotkey.DrawControl();

            string listener = hotkey.inputsHotkey.Listener;
            Hotkeys.Add(listener, hotkey);
            HotkeyCreated?.Invoke(hotkey);
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

        public static void TriggerRaised(string listener, InputsChord input)
        {
            var foregroundProcess = MainWindow.processManager.GetForegroundProcess();

            try
            {
                switch (listener)
                {
                    case "shortcutKeyboard":
                        var uiHostNoLaunch = new ProcessUtils.UIHostNoLaunch();
                        var tipInvocation = (ProcessUtils.ITipInvocation)uiHostNoLaunch;
                        tipInvocation.Toggle(ProcessUtils.GetDesktopWindow());
                        Marshal.ReleaseComObject(uiHostNoLaunch);
                        break;
                    case "shortcutDesktop":
                        InputsManager.KeyPress(new VirtualKeyCode[] { VirtualKeyCode.LWIN, VirtualKeyCode.VK_D });
                        break;
                    case "shortcutESC":
                        if (foregroundProcess != null)
                        {
                            ProcessUtils.SetForegroundWindow(foregroundProcess.MainWindowHandle);
                            InputsManager.KeyPress(VirtualKeyCode.ESCAPE);
                        }
                        break;
                    case "shortcutExpand":
                        if (foregroundProcess != null)
                        {
                            ProcessUtils.SetForegroundWindow(foregroundProcess.MainWindowHandle);
                            InputsManager.KeyStroke(VirtualKeyCode.LMENU, VirtualKeyCode.RETURN);
                        }
                        break;
                    case "shortcutTaskview":
                        InputsManager.KeyPress(new VirtualKeyCode[] { VirtualKeyCode.LWIN, VirtualKeyCode.TAB });
                        break;
                    case "shortcutMainwindow":
                        MainWindow.GetCurrent().SwapWindowState();
                        break;
                    case "shortcutGuide":
                        MainWindow.pipeClient.SendMessage(new PipeClientInput() { sButtons = (ushort)0x0400 });
                        break;
                    default:
                        InputsManager.KeyPress(input.OutputKeys);
                        break;
                }
                
                // play a tune to notify a command was executed
                SystemManager.PlayWindowsMedia("Windows Navigation Start.wav");
                
                // raise an event
                CommandExecuted?.Invoke(listener);
            }
            catch (Exception)
            {
                LogManager.LogError("Failed to parse trigger {0}", listener);
            }
        }

        private static void TriggerUpdated(string listener, InputsChord inputs)
        {
            Application.Current.Dispatcher.Invoke(new Action(() =>
            {
                Hotkey hotkey = Hotkeys[listener];
                hotkey.inputsChord = inputs;

                hotkey.UpdateHotkey(true);

                SerializeHotkey(hotkey, true);
            }));
        }
    }
}
