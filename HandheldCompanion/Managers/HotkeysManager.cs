using ControllerCommon;
using ControllerCommon.Managers;
using ControllerCommon.Utils;
using HandheldCompanion.Managers.Classes;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;

namespace HandheldCompanion.Managers
{
    public static class HotkeysManager
    {
        static string Path;

        public static event HotkeyCreatedEventHandler HotkeyCreated;
        public delegate void HotkeyCreatedEventHandler(Hotkey hotkey);

        public static Dictionary<string, Hotkey> Hotkeys = new();

        static HotkeysManager()
        {
            // initialize path
            Path = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "HandheldCompanion", "hotkeys");
            if (!Directory.Exists(Path))
                Directory.CreateDirectory(Path);

            foreach (InputsHotkey inputhotkey in InputsHotkey.Hotkeys)
            {
                Hotkey hotkey = new Hotkey()
                {
                    hotkey = inputhotkey,
                    chord = new()
                };
                SerializeHotkey(hotkey);
            }

            InputsManager.TriggerUpdated += TriggerUpdated;
            InputsManager.TriggerRaised += TriggerRaised;
        }

        public static void Start()
        {
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
            if (hotkey == null || hotkey.hotkey == null || hotkey.chord == null)
            {
                LogManager.LogError("Could not parse hotkey {0}.", fileName);
                return;
            }

            string listener = hotkey.hotkey.GetListener();
            Hotkeys.Add(listener, hotkey);
            HotkeyCreated?.Invoke(hotkey);
        }

        public static void SerializeHotkey(Hotkey hotkey, bool overwrite = false)
        {
            string listener = hotkey.hotkey.GetListener();

            string settingsPath = System.IO.Path.Combine(Path, $"{listener}.json");
            if (!File.Exists(settingsPath) || overwrite)
            {
                var options = new JsonSerializerOptions { WriteIndented = true };
                string jsonString = JsonSerializer.Serialize(hotkey, options);
                File.WriteAllText(settingsPath, jsonString);
            }
        }

        private static void TriggerRaised(string listener, InputsChord input)
        {
            switch(listener)
            {

            }
        }

        private static void TriggerUpdated(string listener, InputsChord inputs)
        {
            Application.Current.Dispatcher.Invoke(new Action(() =>
            {
                Hotkey hotkey = Hotkeys[listener];
                hotkey.chord = inputs;

                hotkey.UpdateButton();

                hotkey.buttonButton.Style = Application.Current.FindResource("DefaultButtonStyle") as Style;

                SerializeHotkey(hotkey, true);
            }));
        }
    }
}
