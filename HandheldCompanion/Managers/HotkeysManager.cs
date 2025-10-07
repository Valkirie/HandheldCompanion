using HandheldCompanion.Commands;
using HandheldCompanion.Devices;
using HandheldCompanion.Inputs;
using HandheldCompanion.Shared;
using HandheldCompanion.Utils;
using HandheldCompanion.Views;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace HandheldCompanion.Managers;

public class HotkeysManager : IManager
{
    public ConcurrentDictionary<ButtonFlags, Hotkey> hotkeys = new();

    public HotkeysManager()
    {
        // initialize path
        ManagerPath = Path.Combine(App.SettingsPath, "hotkeys");

        // create path
        if (!Directory.Exists(ManagerPath))
            Directory.CreateDirectory(ManagerPath);
    }

    public override void Start()
    {
        if (Status.HasFlag(ManagerStatus.Initializing) || Status.HasFlag(ManagerStatus.Initialized))
            return;

        base.PrepareStart();

        bool IsFirstStart = MainWindow.LastVersion == Version.Parse("0.0.0.0");

        // process existing hotkeys
        string[] fileEntries = Directory.GetFiles(ManagerPath, "*.json", SearchOption.AllDirectories);
        foreach (string fileName in fileEntries)
            ProcessHotkey(fileName);

        // deploy device-default hotkeys during first start
        if (IsFirstStart)
        {
            foreach (KeyValuePair<Type, Hotkey> kvp in IDevice.GetCurrent().DeviceHotkeys)
            {
                Hotkey hotkey = kvp.Value;

                // skip if button flag is already used
                if (hotkeys.Values.Any(hk => hk.ButtonFlags == hotkey.ButtonFlags))
                    continue;

                // skip if command is already used
                if (hotkeys.Values.Any(hk => hk.command.GetType() == hotkey.command.GetType()))
                    continue;

                UpdateOrCreateHotkey(hotkey, UpdateSource.Creation);
            }
        }

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
        UpdateSource updateSource = UpdateSource.Serializer;

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

            if (version == Version.Parse("0.0.0.0"))
            {
                // too old
                throw new Exception("Hotkey is outdated.");
            }
            if (jObject.ContainsKey("hotkeyId"))
            {
                // too old
                throw new Exception("Hotkey is outdated.");
            }

            if (version <= Version.Parse("0.27.0.7"))
            {
                // let's make sure we get a Dictionary
                outputraw = outputraw.Replace(
                    "\"System.Collections.Concurrent.ConcurrentDictionary`2[[HandheldCompanion.Inputs.ButtonFlags, HandheldCompanion],[System.Boolean, System.Private.CoreLib]], System.Collections.Concurrent\"",
                    "\"System.Collections.Generic.Dictionary`2[[HandheldCompanion.Inputs.ButtonFlags, HandheldCompanion],[System.Boolean, System.Private.CoreLib]], System.Private.CoreLib\"");
            }
            if (version <= Version.Parse("0.27.0.13"))
            {
                // Clean legacy/unknown ButtonFlags
                outputraw = StripUnknownButtonFlags(outputraw, out var removed);
            }

            // parse profile
            if (hotkey is null)
                hotkey = JsonConvert.DeserializeObject<Hotkey>(outputraw, new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.All });

            // store fileName
            hotkey.FileName = Path.GetFileName(fileName);
        }
        catch (Exception ex)
        {
            LogManager.LogError("Could not parse profile {0}. {1}", fileName, ex.Message);
            return;
        }

        // check if button flags is already used
        // or in case of updated hotkey naming convention
        if (IsUsedButtonFlag(hotkey.ButtonFlags) || !hotkey.FileName.Equals(hotkey.GetFileName(), StringComparison.InvariantCultureIgnoreCase))
        {
            // update button flags
            hotkey.ButtonFlags = GetAvailableButtonFlag();

            // Delete the old file
            File.Delete(fileName);

            // set update source so UpdateOrCreateHotkey() will (re)create the file
            updateSource = UpdateSource.Creation;
        }

        // we couldn't find a free hotkey slot
        if (hotkey.ButtonFlags == ButtonFlags.None)
            return;

        UpdateOrCreateHotkey(hotkey, updateSource);
    }

    /// <summary>
    /// Removes properties with keys that are not valid current ButtonFlags
    /// from any JSON dictionary keyed by ButtonFlags (profiles or hotkeys).
    /// Returns the cleaned JSON. 'removedCount' is how many entries got dropped.
    /// </summary>
    public static string StripUnknownButtonFlags(string json, out int removedCount)
    {
        var root = JObject.Parse(json, new JsonLoadSettings
        {
            // If a file already had duplicate keys, keep last while loading.
            DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Replace
        });

        removedCount = 0;

        foreach (var obj in root.Descendants().OfType<JObject>())
        {
            if (!LooksLikeButtonFlagsDictionary(obj))
                continue;

            // Safe mutation over a snapshot of properties
            foreach (var prop in obj.Properties().ToList())
            {
                var key = prop.Name;

                // Keep Json.NET metadata like $type
                if (key.StartsWith("$", StringComparison.Ordinal))
                    continue;

                if (IsValidButtonFlagKey(key))
                    continue;

                // Unknown / legacy / typo -> drop it
                prop.Remove();
                removedCount++;
            }
        }

        return root.ToString(Formatting.Indented);
    }

    private static bool LooksLikeButtonFlagsDictionary(JObject obj)
    {
        // Strong signal: the object itself has a $type indicating a Dictionary<ButtonFlags, T>
        if (obj.TryGetValue("$type", out var tkn) && tkn.Type == JTokenType.String)
        {
            var typeStr = (string)tkn!;
            if (typeStr?.Contains("Dictionary`2[[", StringComparison.OrdinalIgnoreCase) == true
                || typeStr?.Contains("SortedDictionary`2[[", StringComparison.OrdinalIgnoreCase) == true)
            {
                if (typeStr.Contains("HandheldCompanion.Inputs.ButtonFlags", StringComparison.OrdinalIgnoreCase))
                    return true;
            }
        }

        // Heuristic fallback: many keys that parse as ButtonFlags names or numeric codes.
        int keys = 0, plausible = 0;
        foreach (var p in obj.Properties())
        {
            if (p.Name.StartsWith("$", StringComparison.Ordinal))
                continue;

            keys++;
            if (IsButtonFlagNameOrNumber(p.Name))
                plausible++;
        }

        // Treat as ButtonFlags dict if enough keys look like ButtonFlags
        return keys > 0 && plausible >= Math.Max(1, keys / 2);
    }

    private static bool IsValidButtonFlagKey(string key)
    {
        // Current enum name?
        if (Enum.TryParse<ButtonFlags>(key, ignoreCase: true, out var parsed)
            && Enum.IsDefined(typeof(ButtonFlags), parsed))
            return true;

        // Numeric string like "66" -> valid if it maps to a defined enum value
        if (byte.TryParse(key, NumberStyles.Integer, CultureInfo.InvariantCulture, out var b)
            && Enum.IsDefined(typeof(ButtonFlags), (ButtonFlags)b))
            return true;

        return false;
    }

    private static bool IsButtonFlagNameOrNumber(string key)
    {
        if (Enum.TryParse<ButtonFlags>(key, ignoreCase: true, out var parsed)
            && Enum.IsDefined(typeof(ButtonFlags), parsed))
            return true;

        if (byte.TryParse(key, NumberStyles.Integer, CultureInfo.InvariantCulture, out var b)
            && Enum.IsDefined(typeof(ButtonFlags), (ButtonFlags)b))
            return true;

        return false;
    }

    private bool IsUsedButtonFlag(ButtonFlags buttonFlags)
    {
        HashSet<ButtonFlags> usedFlags = hotkeys.Values.Select(h => h.ButtonFlags).ToHashSet();
        return usedFlags.Contains(buttonFlags);
    }

    public ButtonFlags GetAvailableButtonFlag()
    {
        HashSet<ButtonFlags> used = hotkeys.Values.Select(h => h.ButtonFlags).ToHashSet();

        for (byte button = (byte)ButtonFlags.HOTKEY_USER0; button <= (byte)ButtonFlags.HOTKEY_USER59; button++)
        {
            ButtonFlags flag = (ButtonFlags)button;
            if (!used.Contains(flag))
                return flag;
        }

        return ButtonFlags.None;
    }

    public void UpdateOrCreateHotkey(Hotkey hotkey, UpdateSource source = UpdateSource.Background)
    {
        hotkeys[hotkey.ButtonFlags] = hotkey;
        Updated?.Invoke(hotkey);

        if (source == UpdateSource.Serializer || hotkey.IsInternal)
            return;

        // serialize profile
        SerializeHotkey(hotkey);
    }

    public void SerializeHotkey(Hotkey hotkey)
    {
        string hotkeyPath = Path.Combine(ManagerPath, $"{hotkey.ButtonFlags}.json");

        // update profile version to current build
        hotkey.Version = MainWindow.CurrentVersion;

        string jsonString = JsonConvert.SerializeObject(hotkey, Formatting.Indented, new JsonSerializerSettings
        {
            TypeNameHandling = TypeNameHandling.All
        });
        if (FileUtils.IsFileWritable(hotkeyPath))
            File.WriteAllText(hotkeyPath, jsonString);
    }

    public void DeleteHotkey(Hotkey hotkey)
    {
        var hotkeyPath = Path.Combine(ManagerPath, $"{hotkey.ButtonFlags}.json");

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