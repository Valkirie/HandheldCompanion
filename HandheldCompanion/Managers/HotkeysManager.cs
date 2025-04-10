using HandheldCompanion.Commands;
using HandheldCompanion.Commands.Functions.HC;
using HandheldCompanion.Commands.Functions.Multimedia;
using HandheldCompanion.Commands.Functions.Multitasking;
using HandheldCompanion.Commands.Functions.Performance;
using HandheldCompanion.Commands.Functions.Windows;
using HandheldCompanion.Inputs;
using HandheldCompanion.Shared;
using HandheldCompanion.Utils;
using HandheldCompanion.Views;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace HandheldCompanion.Managers;

public class HotkeysManager : IManager
{
    public ConcurrentDictionary<ButtonFlags, Hotkey> hotkeys = new();
    private readonly string HotkeysPath;

    public HotkeysManager()
    {
        // initialize path
        HotkeysPath = Path.Combine(App.SettingsPath, "hotkeys");

        // create path
        if (!Directory.Exists(HotkeysPath))
            Directory.CreateDirectory(HotkeysPath);
    }

    public override void Start()
    {
        if (Status.HasFlag(ManagerStatus.Initializing) || Status.HasFlag(ManagerStatus.Initialized))
            return;

        base.PrepareStart();

        // process existing hotkeys
        string[] fileEntries = Directory.GetFiles(HotkeysPath, "*.json", SearchOption.AllDirectories);
        foreach (string fileName in fileEntries)
            ProcessHotkey(fileName);

        // get latest known version
        // if last time HC version used old hotkey engine and user has no defined hotkeys
        Version LastVersion = Version.Parse(ManagerFactory.settingsManager.GetString("LastVersion"));
        if (LastVersion < Version.Parse(Settings.VersionHotkeyManager) && hotkeys.Count == 0)
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

            if (!hotkeys.Values.Any(hotkey => hotkey.command is OnScreenKeyboardCommands onScreenKeyboardCommands))
                UpdateOrCreateHotkey(new Hotkey() { command = new OnScreenKeyboardCommands(), IsPinned = true });
        }

        // mandatory hotkeys
        if (!hotkeys.Values.Any(hotkey => hotkey.command is DesktopLayoutCommands desktopLayoutCommands))
            UpdateOrCreateHotkey(new Hotkey() { command = new DesktopLayoutCommands(), IsPinned = true });

        // manage events
        InputsManager.StoppedListening += InputsManager_StoppedListening;

        base.Start();
    }

    public override void Stop()
    {
        if (Status.HasFlag(ManagerStatus.Halting) || Status.HasFlag(ManagerStatus.Halted))
            return;

        base.PrepareStop();

        // manage events
        InputsManager.StoppedListening -= InputsManager_StoppedListening;

        base.Stop();
    }

    private void InputsManager_StoppedListening(ButtonFlags buttonFlags, InputsChord storedChord)
    {
        // update chord(s)
        if (storedChord.KeyState.Count != 0 || storedChord.ButtonState.Buttons.Count() != 0)
        {
            if (hotkeys.TryGetValue(buttonFlags, out Hotkey hotkey))
            {
                switch (storedChord.chordTarget)
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

    public IEnumerable<Hotkey> GetHotkeys()
    {
        return hotkeys.Values.OrderBy(hotkey => hotkey.PinIndex);
    }

    private void ProcessHotkey(string fileName)
    {
        Hotkey? hotkey = null;

        try
        {
            string rawName = Path.GetFileNameWithoutExtension(fileName);
            if (string.IsNullOrEmpty(rawName))
            {
                LogManager.LogError("Could not parse profile {0}. {1}", fileName, "Profile has an incorrect file name.");
                return;
            }

            string outputraw = File.ReadAllText(fileName);
            JObject? jObject = JObject.Parse(outputraw);

            Version version = new();
            if (jObject.ContainsKey("Version"))
                version = Version.Parse((string)jObject["Version"]);

            try
            {
                if (jObject.ContainsKey("hotkeyId"))
                {
                    // this goes back to 0.21.4.1 ?
                    hotkey = MigrateFrom0_21_4_1(fileName, jObject);
                }
                else if (version == Version.Parse("0.0.0.0"))
                {
                    // too old
                    throw new Exception("Hotkey is outdated.");
                }

                // we've been doing back and forth on ButtonState State type
                // let's make sure we get a ConcurrentDictionary
                outputraw = outputraw.Replace(
                        "\"System.Collections.Generic.Dictionary`2[[HandheldCompanion.Inputs.ButtonFlags, HandheldCompanion],[System.Boolean, System.Private.CoreLib]], System.Private.CoreLib\"",
                        "\"System.Collections.Concurrent.ConcurrentDictionary`2[[HandheldCompanion.Inputs.ButtonFlags, HandheldCompanion],[System.Boolean, System.Private.CoreLib]], System.Collections.Concurrent\"");

                // parse profile
                if (hotkey is null)
                    hotkey = JsonConvert.DeserializeObject<Hotkey>(outputraw, new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.All });
            }
            catch (Exception ex)
            {
                LogManager.LogError("Could not parse hotkey {0}. {1}", fileName, ex.Message);
                return;
            }
        }
        catch (Exception ex)
        {
            LogManager.LogError("Could not parse profile {0}. {1}", fileName, ex.Message);
            return;
        }

        if (hotkey is null)
            return;

        // check if button flags is already used
        if (IsUsedButtonFlag(hotkey.ButtonFlags))
        {
            // update button flags
            hotkey.ButtonFlags = GetAvailableButtonFlag();

            // Delete the old file
            File.Delete(fileName);
        }

        // we couldn't find a free hotkey slot
        if (hotkey.ButtonFlags == ButtonFlags.None)
            return;

        UpdateOrCreateHotkey(hotkey);
    }

    private Hotkey? MigrateFrom0_21_4_1(string fileName, JObject? dictionary)
    {
        ICommands command = new EmptyCommands();

        // This hotkey is from old format, need migrate to new hotkey
        ushort hotkeyId = (ushort)dictionary["hotkeyId"];
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
                    command = new QuickOverlayCommands();
                    break;
                case 10:
                    command = new QuickToolsCommands();
                    break;
                case 11:
                    command = new TDPIncrease();
                    break;
                case 12:
                    command = new TDPDecrease();
                    break;
                case 13:
                    // "suspendResumeTask"
                    break;
                case 20:
                    command = new OnScreenKeyboardCommands();
                    break;
                case 26:
                    command = new KillForegroundCommands();
                    break;
                //case 21-28:
                // "shortcutDesktop", "shortcutESC", "shortcutExpand", "shortcutTaskView", "shortcutTaskManager", "shortcutControlCenter", "shortcutPrintScreen"
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

                // we couldn't find a free hotkey slot
                if (hotkey.ButtonFlags == ButtonFlags.None)
                    return null;

                // Migrate InputsType
                int oldInputsType = (int)dictionary["inputsChord"]["InputsType"];
                if (Enum.TryParse(oldInputsType.ToString(), out InputsChordType oldType))
                {
                    hotkey.inputsChord.chordType = oldType;
                }

                // Migrate Old State
                JObject? oldState = (JObject)dictionary["inputsChord"]["State"]["State"];
                foreach (var keyValuePair in oldState)
                    if (Enum.TryParse(keyValuePair.Key, out ButtonFlags flag))
                        hotkey.inputsChord.ButtonState.State[flag] = (bool)keyValuePair.Value;

                // Migrate IsPinned
                bool isPinned = (bool)dictionary["IsPinned"];
                hotkey.IsPinned = isPinned;

                // Delete the old file
                File.Delete(fileName);

                // Save new hotkey json
                SerializeHotkey(hotkey);

                return hotkey;
            }
        }

        return null;
    }

    private bool IsUsedButtonFlag(ButtonFlags buttonFlags)
    {
        HashSet<ButtonFlags> usedFlags = hotkeys.Values.Select(h => h.ButtonFlags).ToHashSet();
        return usedFlags.Contains(buttonFlags);
    }

    public ButtonFlags GetAvailableButtonFlag()
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

    public void UpdateOrCreateHotkey(Hotkey hotkey)
    {
        hotkeys[hotkey.ButtonFlags] = hotkey;
        Updated?.Invoke(hotkey);

        // serialize profile
        if (!hotkey.IsInternal)
            SerializeHotkey(hotkey);
    }

    public void SerializeHotkey(Hotkey hotkey)
    {
        string hotkeyPath = Path.Combine(HotkeysPath, $"{hotkey.ButtonFlags}.json");

        // update profile version to current build
        hotkey.Version = new Version(MainWindow.fileVersionInfo.FileVersion);

        string jsonString = JsonConvert.SerializeObject(hotkey, Formatting.Indented, new JsonSerializerSettings
        {
            TypeNameHandling = TypeNameHandling.All
        });
        if (FileUtils.IsFileWritable(hotkeyPath))
            File.WriteAllText(hotkeyPath, jsonString);
    }

    public void DeleteHotkey(Hotkey hotkey)
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

    #region events

    public event DeletedEventHandler Deleted;
    public delegate void DeletedEventHandler(Hotkey hotkey);

    public event UpdatedEventHandler Updated;
    public delegate void UpdatedEventHandler(Hotkey hotkey);

    #endregion
}