using HandheldCompanion.Commands;
using HandheldCompanion.Commands.Functions.HC;
using HandheldCompanion.Commands.Functions.Windows;
using HandheldCompanion.Inputs;
using HandheldCompanion.Utils;
using HandheldCompanion.Views;
using Newtonsoft.Json;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using HandheldCompanion.Commands.Functions.Multimedia;
using Newtonsoft.Json.Linq;

namespace HandheldCompanion.Managers;

public static class HotkeysManager
{
    public static ConcurrentDictionary<ButtonFlags, Hotkey> hotkeys = new();

    private static readonly string HotkeysPath;

    private static bool IsInitialized;

    static HotkeysManager()
    {
        // initialize path
        HotkeysPath = Path.Combine(MainWindow.SettingsPath, "hotkeys");
        if (!Directory.Exists(HotkeysPath))
            Directory.CreateDirectory(HotkeysPath);

        InputsManager.StoppedListening += InputsManager_StoppedListening;
    }

    private static void InputsManager_StoppedListening(ButtonFlags buttonFlags, InputsChord storedChord)
    {
        // update chord(s)
        if (storedChord.KeyState.Count != 0 || storedChord.ButtonState.Buttons.Count() != 0)
        {
            if (hotkeys.TryGetValue(buttonFlags, out Hotkey hotkey))
            {
                switch(storedChord.chordTarget)
                {
                    case InputsChordTarget.Input:
                        hotkey.inputsChord = storedChord.Clone() as InputsChord;
                        break;
                    case InputsChordTarget.Output:
                        if (hotkey.command is KeyboardCommands keyboardCommands)
                            keyboardCommands.outputChord = storedChord.Clone() as InputsChord;
                        break;
                }

                UpdateOrCreateHotkey(hotkey);
            }
        }
    }

    public static ICollection<Hotkey> GetHotkeys()
    {
        return hotkeys.Values;
    }

    public static void Start()
    {
        // process existing hotkeys
        string[] fileEntries = Directory.GetFiles(HotkeysPath, "*.json", SearchOption.AllDirectories);
        foreach (string fileName in fileEntries)
            ProcessHotkey(fileName);

        // get latest known version
        Version LastVersion = Version.Parse(SettingsManager.GetString("LastVersion"));
        if (LastVersion < Version.Parse(Settings.VersionHotkeyManager))
        {
            // create a few defaults hotkeys
            if (!hotkeys.Values.Any(hotkey => hotkey.command is QuickToolsCommands quickToolsCommands))
                UpdateOrCreateHotkey(new Hotkey() { command = new QuickToolsCommands() });

            if (!hotkeys.Values.Any(hotkey => hotkey.command is MainWindowCommands mainWindowCommands))
                UpdateOrCreateHotkey(new Hotkey() { command = new MainWindowCommands() });

            if (!hotkeys.Values.Any(hotkey => hotkey.command is OverlayGamepadCommands overlayGamepadCommands))
                UpdateOrCreateHotkey(new Hotkey() { command = new OverlayGamepadCommands(), IsPinned = true });

            if (!hotkeys.Values.Any(hotkey => hotkey.command is OverlayTrackpadCommands overlayTrackpadCommands))
                UpdateOrCreateHotkey(new Hotkey() { command = new OverlayTrackpadCommands(), IsPinned = true });

            if (!hotkeys.Values.Any(hotkey => hotkey.command is DesktopLayoutCommands desktopLayoutCommands))
                UpdateOrCreateHotkey(new Hotkey() { command = new DesktopLayoutCommands(), IsPinned = true });

            if (!hotkeys.Values.Any(hotkey => hotkey.command is OnScreenKeyboardCommands onScreenKeyboardCommands))
                UpdateOrCreateHotkey(new Hotkey() { command = new OnScreenKeyboardCommands(), IsPinned = true });
        }

        IsInitialized = true;
        Initialized?.Invoke();
    }

    private static void ProcessHotkey(string fileName)
    {
        Hotkey? hotkey = null;

        // Check json filename, if it is not a number (as ButtonFlags), it is the old format
        var regex = new Regex(@"(\d+)\.json", RegexOptions.IgnoreCase);
        var match = regex.Match(fileName);

        if (!match.Success)
        {
            try
            {
                hotkey = MigrateHotkey(fileName);
            }
            catch (Exception ex)
            {
                LogManager.LogError("Could not migrate old hotkey {0}. {1}", fileName, ex.Message);
                return;
            }

            if (hotkey is null)
            {
                LogManager.LogError("Could not migrate old hotkey {0}.", fileName);
                return;
            }
        }
        else
        {
            try
            {
                var outputraw = File.ReadAllText(fileName);
                hotkey = JsonConvert.DeserializeObject<Hotkey>(outputraw, new JsonSerializerSettings
                {
                    TypeNameHandling = TypeNameHandling.All
                });
            }
            catch (Exception ex)
            {
                LogManager.LogError("Could not parse hotkey {0}. {1}", fileName, ex.Message);
            }
        }

        if (hotkey is null)
            return;

        if (CheckAvailableButtonFlag(hotkey.ButtonFlags))
            hotkey.ButtonFlags = GetAvailableButtonFlag();

        if (hotkey.ButtonFlags == ButtonFlags.None)
            return;

        hotkeys[hotkey.ButtonFlags] = hotkey;
        Updated?.Invoke(hotkey);
    }

    private static Hotkey? MigrateHotkey(string fileName)
    {
        var json = File.ReadAllText(fileName);
        JObject? dictionary = JObject.Parse(json);

        if (!dictionary.ContainsKey("hotkeyId"))
        {
            return null;
        }

        ushort hotkeyId = (ushort)dictionary["hotkeyId"];

        ICommands command = new EmptyCommands();

        // This hotkey is from old format, need migrate to new hotkey
        if (hotkeyId > 0)
        {
            switch (hotkeyId)
            {
                case 01:
                    command = new OverlayGamepadCommands();
                    break;
                case 02:
                    command = new OverlayTrackpadCommands();
                    break;
                case 03:
                    // "OnScreenDisplayToggle"
                    break;
                case 10:
                    command = new QuickToolsCommands();
                    break;
                case 11:
                    // "increaseTDP"
                    break;
                case 12:
                    // "decreaseTDP"
                    break;
                case 13:
                    // "suspendResumeTask"
                    break;
                case 20:
                    command = new OnScreenKeyboardCommands();
                    break;
                //case 21-28:
                // "shortcutDesktop", "shortcutESC", "shortcutExpand", "shortcutTaskView", "shortcutTaskManager", "shortcutKillApp", "shortcutControlCenter", "shortcutPrintScreen"
                //break;
                case 30:
                    command = new MainWindowCommands();
                    break;
                case 31:
                    command = new DesktopLayoutCommands();
                    break;
                case 32:
                    command = new HIDModeCommands();
                    break;
                case 33:
                case 34:
                    command = new CycleSubProfileCommands();
                    break;
                case 41:
                    command = new BrightnessIncrease();
                    break;
                case 42:
                    command = new BrightnessDecrease();
                    break;
                case 43:
                    command = new VolumeIncrease();
                    break;
                case 44:
                    command = new VolumeDecrease();
                    break;
            }

            // Check if the above switch is handled, migrate to new hotkey and delete old file
            if (command is not EmptyCommands)
            {
                Hotkey hotkey = new Hotkey();
                hotkey.command = command;

                // Migrate InputsType
                var oldInputsType = (int)dictionary["inputsChord"]["InputsType"];
                if (Enum.TryParse(oldInputsType.ToString(), out InputsChordType oldType))
                {
                    hotkey.inputsChord.chordType = oldType;
                }

                // Migrate Old State
                var oldState = (JObject)dictionary["inputsChord"]["State"]["State"];
                foreach (var keyValuePair in oldState)
                {
                    if (Enum.TryParse(keyValuePair.Key, out ButtonFlags flag))
                    {
                        hotkey.inputsChord.ButtonState.State[flag] = (bool)keyValuePair.Value;
                    }
                }

                // Common ButtonFlags check
                if (CheckAvailableButtonFlag(hotkey.ButtonFlags))
                    hotkey.ButtonFlags = GetAvailableButtonFlag();

                if (hotkey.ButtonFlags == ButtonFlags.None)
                    return null;

                // Delete the old file
                File.Delete(fileName);

                // Save new hotkey json
                SerializeHotkey(hotkey);

                return hotkey;
            }
        }

        return null;
    }

    private static bool CheckAvailableButtonFlag(ButtonFlags buttonFlags)
    {
        HashSet<ButtonFlags> usedFlags = hotkeys.Values.Select(h => h.ButtonFlags).ToHashSet();
        return usedFlags.Contains(buttonFlags);
    }

    public static ButtonFlags GetAvailableButtonFlag()
    {
        HashSet<ButtonFlags> usedFlags = hotkeys.Values.Select(h => h.ButtonFlags).ToHashSet();
        foreach (byte flagValue in Enumerable.Range((int)ButtonFlags.HOTKEY_START + 1, (int)ButtonFlags.HOTKEY_END - (int)ButtonFlags.HOTKEY_START + 2))
        {
            ButtonFlags flag = (ButtonFlags)flagValue;
            if (!usedFlags.Contains(flag))
            {
                return flag;
            }
        }

        return ButtonFlags.None;
    }

    public static void UpdateOrCreateHotkey(Hotkey hotkey)
    {
        hotkeys[hotkey.ButtonFlags] = hotkey;
        Updated?.Invoke(hotkey);

        // serialize profile
        if (!hotkey.IsInternal)
            SerializeHotkey(hotkey);
    }

    public static void SerializeHotkey(Hotkey hotkey)
    {
        var hotkeyPath = Path.Combine(HotkeysPath, $"{hotkey.ButtonFlags}.json");
        var jsonString = JsonConvert.SerializeObject(hotkey, Formatting.Indented, new JsonSerializerSettings
        {
            TypeNameHandling = TypeNameHandling.All
        });
        if (FileUtils.IsFileWritable(hotkeyPath))
            File.WriteAllText(hotkeyPath, jsonString);
    }

    public static void DeleteHotkey(Hotkey hotkey)
    {
        var hotkeyPath = Path.Combine(HotkeysPath, $"{hotkey.ButtonFlags}.json");

        if (hotkeys.ContainsKey(hotkey.ButtonFlags))
        {
            _ = hotkeys.TryRemove(hotkey.ButtonFlags, out Hotkey removedValue);

            // raise event(s)
            Deleted?.Invoke(hotkey);
        }

        FileUtils.FileDelete(hotkeyPath);
    }

    public static void Stop()
    {
        IsInitialized = false;
    }

    #region events

    public static event InitializedEventHandler Initialized;
    public delegate void InitializedEventHandler();

    public static event DeletedEventHandler Deleted;
    public delegate void DeletedEventHandler(Hotkey hotkey);

    public static event UpdatedEventHandler Updated;
    public delegate void UpdatedEventHandler(Hotkey hotkey);

    #endregion
}