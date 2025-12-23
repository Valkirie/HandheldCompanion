using HandheldCompanion.Actions;
using HandheldCompanion.Controllers;
using HandheldCompanion.Devices;
using HandheldCompanion.Helpers;
using HandheldCompanion.Misc;
using HandheldCompanion.Properties;
using HandheldCompanion.Shared;
using HandheldCompanion.Utils;
using HandheldCompanion.Views;
using iNKORE.UI.WPF.Modern.Controls;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Stfu.Linq;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using static HandheldCompanion.Misc.ProcessEx;
using static HandheldCompanion.Utils.XInputPlusUtils;

namespace HandheldCompanion.Managers;

public class ProfileManager : IManager
{
    public const string DefaultName = "Default";

    public ConcurrentDictionary<Guid, Profile> profiles = new();
    // public List<Profile> subProfiles = [];

    private object profileLock = new();
    private Profile currentProfile;

    public FileSystemWatcher profileWatcher { get; set; }

    public ProfileManager()
    {
        // initialize path
        ManagerPath = Path.Combine(App.SettingsPath, "profiles");

        // create path
        if (!Directory.Exists(ManagerPath))
            Directory.CreateDirectory(ManagerPath);

        // monitor profile files
        profileWatcher = new FileSystemWatcher
        {
            Path = ManagerPath,
            EnableRaisingEvents = true,
            IncludeSubdirectories = true,
            Filter = "*.json",
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.Size
        };
    }

    // match the standard GUID pattern
    private Regex guidRegex = new Regex(@"[0-9A-Fa-f]{8}(-[0-9A-Fa-f]{4}){3}-[0-9A-Fa-f]{12}");
    public override void Start()
    {
        if (Status.HasFlag(ManagerStatus.Initializing) || Status.HasFlag(ManagerStatus.Initialized))
            return;

        base.PrepareStart();

        // process existing profiles
        string[] fileEntries = Directory.GetFiles(ManagerPath, "*.json", SearchOption.AllDirectories);
        string[] sorted = fileEntries.OrderBy(f => guidRegex.IsMatch(Path.GetFileNameWithoutExtension(f)))
            .ThenBy(f => Path.GetFileNameWithoutExtension(f)).ToArray();

        foreach (string fileName in sorted)
            ProcessProfile(fileName, false);

        // check for default profile
        if (!HasDefault())
        {
            // get default layout
            Layout defaultLayout = IDevice.GetCurrent().DefaultLayout.Clone() as Layout;

            Profile defaultProfile = new()
            {
                Name = DefaultName,
                Default = true,
                Enabled = false,
                Layout = defaultLayout,
                LayoutTitle = LayoutTemplate.DefaultLayout.Name,
            };

            // get default power profiles
            PowerProfile? betterPeformanceProfile = IDevice.GetCurrent().DevicePowerProfiles.FirstOrDefault(p => p.Guid == IDevice.BetterPerformanceGuid);
            PowerProfile? bestPeformanceProfile = IDevice.GetCurrent().DevicePowerProfiles.FirstOrDefault(p => p.Guid == IDevice.BestPerformanceGuid);
            defaultProfile.PowerProfiles[(int)PowerLineStatus.Offline] = betterPeformanceProfile?.Guid ?? Guid.Empty;
            defaultProfile.PowerProfiles[(int)PowerLineStatus.Online] = bestPeformanceProfile?.Guid ?? Guid.Empty;

            UpdateOrCreateProfile(defaultProfile, UpdateSource.Creation);
        }

        // profile watcher events
        profileWatcher.Created += ProfileCreated;
        profileWatcher.Deleted += ProfileDeleted;

        // manage events
        ManagerFactory.processManager.ForegroundChanged += ProcessManager_ForegroundChanged;
        ManagerFactory.processManager.ProcessStarted += ProcessManager_ProcessStarted;
        ManagerFactory.processManager.ProcessStopped += ProcessManager_ProcessStopped;
        ManagerFactory.powerProfileManager.Deleted += PowerProfileManager_Deleted;
        ControllerManager.ControllerPlugged += ControllerManager_ControllerPlugged;

        // raise events
        switch (ManagerFactory.processManager.Status)
        {
            default:
            case ManagerStatus.Initializing:
                ManagerFactory.processManager.Initialized += ProcessManager_Initialized;
                break;
            case ManagerStatus.Initialized:
                QueryForeground();
                break;
        }

        if (ControllerManager.IsInitialized)
            ControllerManager_ControllerPlugged(ControllerManager.GetTarget(), false);

        base.Start();
    }

    private void QueryForeground()
    {
        ProcessEx processEx = ProcessManager.GetCurrent();
        if (processEx is null)
            return;

        ProcessFilter filter = ProcessManager.GetFilter(processEx.Executable, processEx.Path);
        ProcessManager_ForegroundChanged(processEx, null, filter);
    }

    private void ProcessManager_Initialized()
    {
        QueryForeground();
    }

    public override void Stop()
    {
        if (Status.HasFlag(ManagerStatus.Halting) || Status.HasFlag(ManagerStatus.Halted))
            return;

        base.PrepareStop();

        // profile watcher events
        profileWatcher.Created -= ProfileCreated;
        profileWatcher.Deleted -= ProfileDeleted;

        // manage events
        ManagerFactory.processManager.ForegroundChanged -= ProcessManager_ForegroundChanged;
        ManagerFactory.processManager.ProcessStarted -= ProcessManager_ProcessStarted;
        ManagerFactory.processManager.ProcessStopped -= ProcessManager_ProcessStopped;
        ManagerFactory.processManager.Initialized -= ProcessManager_Initialized;
        ManagerFactory.powerProfileManager.Deleted -= PowerProfileManager_Deleted;
        ControllerManager.ControllerPlugged -= ControllerManager_ControllerPlugged;

        base.Stop();
    }

    public bool Contains(Profile profile)
    {
        foreach (var pr in profiles.Values)
            if (pr.Path.Equals(profile.Path, StringComparison.InvariantCultureIgnoreCase))
                return true;

        return false;
    }

    public bool Contains(string fileName)
    {
        foreach (var pr in profiles.Values)
            if (pr.Path.Equals(fileName, StringComparison.InvariantCultureIgnoreCase))
                return true;

        return false;
    }

    public Profile GetProfileFromPath(string path, bool ignoreStatus = true, bool getParent = false)
    {
        if (string.IsNullOrEmpty(path))
            return GetDefault();

        Profile profile = null;

        // Get favorite sub-profile (unless asking for parent)
        if (!getParent)
            profile = profiles.Values.FirstOrDefault(p => p.IsSubProfile && (p.Path.Equals(path, StringComparison.InvariantCultureIgnoreCase) || p.Executables.Contains(path)) && p.IsFavoriteSubProfile);

        // If null, get profile by path
        var parentProfiles = profiles.Values.Where(p => !p.IsSubProfile);
        profile ??= parentProfiles.FirstOrDefault(p => !p.IsSubProfile && (p.Path.Equals(path, StringComparison.InvariantCultureIgnoreCase) || p.Executables.Contains(path)));

        // If null, get profile by executable name
        // todo: improve me, maybe scroll through all string instead ?
        if (profile is null)
        {
            string exeName = Path.GetFileName(path);
            profile = profiles.Values.FirstOrDefault(p => (p.Path.Equals(path, StringComparison.InvariantCultureIgnoreCase) || p.Executables.Contains(exeName)));
        }

        // If null or disabled, return default
        if (profile is null || (!ignoreStatus && !profile.Enabled))
            return GetDefault();

        return profile;
    }

    public Profile GetProfileFromGuid(Guid Guid, bool ignoreStatus = true, bool isSubProfile = false)
    {
        Profile profile = null;

        // get profile from Guid
        profile = profiles.Values.FirstOrDefault(pr => pr.Guid == Guid);

        // get profile from path
        if (profile is null)
            return GetDefault();

        // ignore profile status (enabled/disabled)
        if (ignoreStatus)
            return profile;

        return profile.Enabled ? profile : GetDefault();
    }

    public IEnumerable<Profile> GetSubProfilesFromProfile(Profile mainProfile, bool addMain = false)
    {
        // get subprofile corresponding to path
        IEnumerable<Profile> filteredSubProfiles = profiles.Values.Where(pr => pr.IsSubProfile && pr.ParentGuid == mainProfile.Guid).OrderBy(pr => pr.Name);
        if (addMain) filteredSubProfiles = filteredSubProfiles.InsertAt(mainProfile, 0);
        return filteredSubProfiles;
    }

    public IEnumerable<Profile> GetSubProfilesFromProfile(Guid guid, bool addMain = false)
    {
        // get subprofile corresponding to path
        IEnumerable<Profile> filteredSubProfiles = profiles.Values.Where(pr => pr.IsSubProfile && pr.ParentGuid == guid).OrderBy(pr => pr.Name);
        if (addMain)
        {
            Profile mainProfile = GetProfileFromGuid(guid, true);
            filteredSubProfiles = filteredSubProfiles.InsertAt(mainProfile, 0);
        }

        return filteredSubProfiles;
    }

    public Profile GetParent(Profile subProfile)
    {
        // if passed in profile is main profile
        if (!subProfile.IsSubProfile || !profiles.ContainsKey(subProfile.Guid) || !profiles.ContainsKey(subProfile.ParentGuid))
            return subProfile;

        // get the main profile if it exists/loaded .. else return the profile itself
        return GetProfileFromGuid(subProfile.ParentGuid);
    }

    public void SetFavorite(Profile subProfile)
    {
        Guid guid = subProfile.IsSubProfile ? subProfile.ParentGuid : subProfile.Guid;

        // update favorite flag across all subprofiles sharing the same parent
        foreach (Profile profile in GetSubProfilesFromProfile(guid))
        {
            profile.IsFavoriteSubProfile = profile.Guid == subProfile.Guid ? true : false;
            SerializeProfile(profile);
        }
    }

    public void CycleSubProfiles(bool previous = false)
    {
        lock (profileLock)
        {
            if (currentProfile == null)
                return;

            // called using previousSubProfile/nextSubProfile hotkeys
            List<Profile> subProfiles = GetSubProfilesFromProfile(currentProfile.ParentGuid).ToList();

            // if profile does not have sub profiles -> do nothing
            if (subProfiles.Count <= 1)
                return;

            // get index of currently applied profile
            int currentIndex = subProfiles.IndexOf(currentProfile);
            int newIndex = currentIndex;

            // previous? decrement, next? increment
            if (previous)
                newIndex -= 1;
            else
                newIndex += 1;

            // ensure index is within list bounds, wrap if needed
            newIndex = (newIndex + subProfiles.Count) % subProfiles.Count;

            // if for whatever reason index is out of bound -> return
            if (newIndex < 0 || newIndex >= subProfiles.Count)
                return;

            // apply profile
            Profile profileToApply = subProfiles[newIndex];
            UpdateOrCreateProfile(profileToApply);
        }
    }

    private void ApplyProfile(Profile profile, UpdateSource source = UpdateSource.Background, bool announce = true)
    {
        // might not be the same anymore if disabled
        profile = GetProfileFromGuid(profile.Guid, false, profile.IsSubProfile);

        lock (profileLock)
        {
            // we've already announced this profile
            if (currentProfile is not null)
                if (currentProfile.Guid == profile.Guid)
                    announce = false;

            // update current profile before invoking event
            currentProfile = profile;
        }

        // refresh error code
        SanitizeProfile(profile);

        // raise event
        Applied?.Invoke(profile, source);

        if (announce)
        {
            if (!profile.IsSubProfile)
            {
                // Log and toast a regular profile announcement
                LogManager.LogInformation("Profile {0} applied", profile.Name);
                ToastManager.SendToast($"Profile {profile.Name} applied");
            }
            else
            {
                // For subprofiles, get the main profile name first
                string mainProfileName = GetParent(profile).Name;
                LogManager.LogInformation("Subprofile {0} {1} applied", mainProfileName, profile.Name);
                ToastManager.SendToast($"Subprofile {mainProfileName} {profile.Name} applied");
            }
        }
    }

    private void PowerProfileManager_Deleted(PowerProfile powerProfile)
    {
        Profile profileToApply = null;

        // update main profiles
        foreach (Profile profile in profiles.Values)
        {
            bool isCurrent = profile.PowerProfiles[(int)PowerLineStatus.Online] == powerProfile.Guid || profile.PowerProfiles[(int)PowerLineStatus.Offline] == powerProfile.Guid;
            if (isCurrent)
            {
                // update profile
                UpdateOrCreateProfile(profile);

                lock (profileLock)
                {
                    if (currentProfile.Path.Equals(profile.Path, StringComparison.InvariantCultureIgnoreCase))
                        profileToApply = profile;
                }
            }
        }

        if (profileToApply != null)
            ApplyProfile(profileToApply);
    }

    private void ProcessManager_ProcessStopped(ProcessEx processEx)
    {
        try
        {
            Profile profile = GetProfileFromPath(processEx.Path, true);
            if (profile.Default)
                return;

            bool isCurrent = profile.ErrorCode.HasFlag(ProfileErrorCode.Running);

            // raise event
            Discarded?.Invoke(profile, isCurrent, GetDefault());

            if (isCurrent)
            {
                // update profile
                UpdateOrCreateProfile(profile);

                // restore default profile
                ApplyProfile(GetDefault());
            }
        }
        catch
        {
        }
    }

    private void ProcessManager_ProcessStarted(ProcessEx processEx, bool OnStartup)
    {
        try
        {
            Profile profile = GetProfileFromPath(processEx.Path, true);
            if (profile.Default)
                return;

            // update vars
            if (profile.LastUsed != processEx.Process.StartTime || !string.Equals(profile.Path, processEx.Path, StringComparison.OrdinalIgnoreCase))
            {
                profile.LastUsed = processEx.Process.StartTime;
                profile.Path = processEx.Path;

                // update profile
                UpdateOrCreateProfile(profile);
            }
        }
        catch { }
    }

    private void ProcessManager_ForegroundChanged(ProcessEx? processEx, ProcessEx? backgroundEx, ProcessFilter filter)
    {
        switch (filter)
        {
            case ProcessFilter.HandheldCompanion:
                return;
        }

        try
        {
            if (processEx is null)
                return;

            Profile? profile = GetProfileFromPath(processEx.Path, false);

            if (profile is null)
                return;

            // skip if current
            lock (profileLock)
            {
                if (profile.Guid == currentProfile?.Guid)
                    return;
            }

            if (!profile.Default)
            {
                // update vars
                if (profile.LastUsed != processEx.Process.StartTime || !string.Equals(profile.Path, processEx.Path, StringComparison.OrdinalIgnoreCase))
                {
                    profile.LastUsed = processEx.Process.StartTime;
                    profile.Path = processEx.Path;

                    // update profile
                    UpdateOrCreateProfile(profile);
                }
            }

            // raise event
            if (backgroundEx is not null)
            {
                Profile backProfile = GetProfileFromPath(backgroundEx.Path, false);
                if (!backProfile.Guid.Equals(profile.Guid))
                    Discarded?.Invoke(backProfile, true, profile);
            }

            ApplyProfile(profile);
        }
        catch { }
    }

    private void ProfileCreated(object sender, FileSystemEventArgs e)
    {
        if (pendingCreation.Contains(e.FullPath))
        {
            pendingCreation.Remove(e.FullPath);
            return;
        }

        ProcessProfile(e.FullPath, true);
    }

    private void ProfileDeleted(object sender, FileSystemEventArgs e)
    {
        if (pendingDeletion.Contains(e.FullPath))
        {
            pendingDeletion.Remove(e.FullPath);
            return;
        }

        // not ideal
        string ProfileName = e.Name.Replace(".json", "");
        Profile? profile = profiles.Values.FirstOrDefault(p => p.Name.Equals(ProfileName, StringComparison.InvariantCultureIgnoreCase));

        // couldn't find a matching profile
        if (profile is null)
            return;

        // you can't delete default profile !
        if (profile.Default)
        {
            SerializeProfile(profile);
            return;
        }

        DeleteProfile(profile);
    }

    private bool HasDefault()
    {
        return profiles.Values.Count(a => a.Default) != 0;
    }

    public Profile GetDefault()
    {
        // Try to find the default profile; if none is found, fall back to a new Profile
        Profile? defaultProfile = profiles.Values.Where(p => p.Default).FirstOrDefault();
        return defaultProfile ?? new Profile();
    }

    public Profile GetCurrent()
    {
        lock (profileLock)
        {
            if (currentProfile is not null)
                return currentProfile;

            return GetDefault();
        }
    }

    public List<Profile> GetProfiles(bool addSub = false)
    {
        List<Profile> profiles = this.profiles.Values.Where(pr => !pr.IsSubProfile).ToList();
        if (addSub)
            profiles.AddRange(GetSubProfiles());

        return profiles;
    }

    public List<Profile> GetSubProfiles()
    {
        return profiles.Values.Where(pr => pr.IsSubProfile).ToList();
    }

    private void ProcessProfile(string fileName, bool imported = false)
    {
        Profile profile;
        UpdateSource updateSource = UpdateSource.Serializer;

        try
        {
            string rawName = Path.GetFileNameWithoutExtension(fileName);
            if (string.IsNullOrEmpty(rawName))
            {
                LogManager.LogError("Could not parse profile: {0}. {1}", fileName, "Profile has an incorrect file name.");
                return;
            }

            string outputraw = File.ReadAllText(fileName);
            JObject jObject = JObject.Parse(outputraw);

            // latest pre-versionning release
            Version version = new();
            if (jObject.TryGetValue("Version", out var value))
                version = new Version(value.ToString());

            // pre-parse manipulations
            if (version == Version.Parse("0.0.0.0"))
            {
                // too old
                throw new Exception("Profile is outdated.");
            }
            else if (version <= Version.Parse("0.22.1.5"))
            {
                // Navigate to the Layout object.
                JObject layout = jObject["Layout"] as JObject;
                if (layout != null)
                {
                    // Navigate to the AxisLayout section.
                    JObject axisLayout = layout["AxisLayout"] as JObject;
                    if (axisLayout != null)
                    {
                        // Replace the overall "$type" if it matches the old type.
                        string oldAxisLayoutType = "System.Collections.Generic.SortedDictionary`2[[HandheldCompanion.Inputs.AxisLayoutFlags, HandheldCompanion],[HandheldCompanion.Actions.IActions, HandheldCompanion]], System.Collections";
                        string newAxisLayoutType = "System.Collections.Generic.SortedDictionary`2[[HandheldCompanion.Inputs.AxisLayoutFlags, HandheldCompanion],[System.Collections.Generic.List`1[[HandheldCompanion.Actions.IActions, HandheldCompanion]], System.Private.CoreLib]], System.Collections";
                        if (axisLayout["$type"] != null && axisLayout["$type"].Type == JTokenType.String)
                        {
                            string currentType = axisLayout["$type"].ToString();
                            if (currentType == oldAxisLayoutType)
                            {
                                axisLayout["$type"] = newAxisLayoutType;
                            }
                        }

                        // Process each property in AxisLayout (skip "$type").
                        foreach (var property in axisLayout.Properties().Where(p => p.Name != "$type").ToList())
                        {
                            // If the value is a JObject, check if it already has a "$values" array.
                            if (property.Value.Type == JTokenType.Object)
                            {
                                JObject valueObj = (JObject)property.Value;
                                if (valueObj["$values"] == null)
                                {
                                    // It is a single IActions object, so wrap it.
                                    JToken singleValue = valueObj.DeepClone();
                                    JObject newArrayObj = new JObject();
                                    newArrayObj["$type"] = "System.Collections.Generic.List`1[[HandheldCompanion.Actions.IActions, HandheldCompanion]], System.Private.CoreLib";
                                    newArrayObj["$values"] = new JArray(singleValue);
                                    property.Value = newArrayObj;
                                }
                                // If it already has "$values", assume it's in the new format.
                            }
                            else
                            {
                                // For any other token type, wrap it in the new array format.
                                JToken singleValue = property.Value.DeepClone();
                                JObject newArrayObj = new JObject();
                                newArrayObj["$type"] = "System.Collections.Generic.List`1[[HandheldCompanion.Actions.IActions, HandheldCompanion]], System.Private.CoreLib";
                                newArrayObj["$values"] = new JArray(singleValue);
                                property.Value = newArrayObj;
                            }
                        }

                        // Convert the modified JObject back to a JSON string.
                        outputraw = jObject.ToString();
                    }
                }
            }
            if (version <= Version.Parse("0.26.0.2"))
            {
                // get previous path, if any
                string path = jObject.GetValue("Path")?.ToString() ?? string.Empty;
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
                outputraw = HotkeysManager.StripUnknownButtonFlags(outputraw, out var removed);
            }

            // parse profile
            profile = JsonConvert.DeserializeObject<Profile>(outputraw, new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.All });

            // store fileName
            profile.FileName = Path.GetFileName(fileName);

            // post-parse manipulations
            if (version <= Version.Parse("0.21.5.4"))
            {
                // Access the PowerProfile value
                string oldPowerProfile = jObject["PowerProfile"]?.ToString();
                if (!string.IsNullOrEmpty(oldPowerProfile))
                {
                    for (int idx = 0; idx < 2; idx++)
                    {
                        Guid powerProfile = profile.PowerProfiles[idx];
                        if (powerProfile == Guid.Empty)
                            profile.PowerProfiles[idx] = new Guid(oldPowerProfile);
                    }
                }
            }

            // hacky, fix broken profiles with Turbo and Toggle enabled across all actions
            // the elements inside (IActions instances) are the same references as the ones stored in profile.Layout.ButtonLayout
            List<IActions> allActions = profile.Layout.ButtonLayout.Values.SelectMany(list => list).ToList();
            if (allActions.Any() && allActions.All(a => a.HasTurbo && a.HasToggle))
            {
                foreach (IActions action in allActions)
                {
                    action.HasTurbo = false;
                    action.HasToggle = false;
                }
            }

            // in case of updated profile naming convention
            if (!profile.FileName.Equals(profile.GetFileName(), StringComparison.InvariantCultureIgnoreCase))
            {
                // delete current file
                File.Delete(fileName);

                // set update source so UpdateOrCreateProfile() will (re)create the file
                updateSource = UpdateSource.Creation;
            }

            // use ParentGuid
            if (profile.IsSubProfile && profile.ParentGuid == Guid.Empty && !string.IsNullOrEmpty(profile.Path))
                profile.ParentGuid = GetProfileFromPath(profile.Path, true, true).Guid;
        }
        catch (Exception ex)
        {
            LogManager.LogError("Could not parse profile: {0}. {1}", fileName, ex.Message);
            return;
        }

        // failed to parse
        if (!profile.Default && (string.IsNullOrEmpty(profile.Name) || string.IsNullOrEmpty(profile.Path)))
        {
            LogManager.LogError("Corrupted profile: {0}. Profile has an empty name or an empty path.", fileName);
            return;
        }

        if (imported)
        {
            bool skipImported = true;
            ManualResetEventSlim waitHandle = new ManualResetEventSlim(false);

            // UI thread
            UIHelper.TryInvoke(async () =>
            {
                // todo: localize me
                Task<ContentDialogResult> dialogTask = new Dialog(MainWindow.GetCurrent())
                {
                    Title = $"Importing profile for {profile.Name}",
                    Content = $"Would you like to import this profile to your database ?",
                    PrimaryButtonText = Resources.ProfilesPage_OK,
                    CloseButtonText = Resources.ProfilesPage_Cancel
                }.ShowAsync();

                ContentDialogResult result = await dialogTask; // await the task

                switch (result)
                {
                    case ContentDialogResult.Primary:
                        skipImported = false;
                        break;
                    default:
                        skipImported = true;
                        break;
                }

                // Signal the waiting thread that the dialog has been closed
                waitHandle.Set();
            });

            // Wait until the dialog has been closed
            waitHandle.Wait();

            // delete file and exit if user decided to skip this profile
            if (skipImported)
            {
                File.Delete(fileName);
                return;
            }
        }

        // if a profile for this path exists, make this one a subprofile
        bool alreadyExist = Contains(profile.Path);
        if (alreadyExist && !profile.IsSubProfile)
        {
            // delete current file
            File.Delete(fileName);

            // skip if identical
            Profile mainProfile = GetProfileFromPath(profile.Path, true, true);
            if (mainProfile.Arguments.Equals(profile.Arguments))
                return;

            // give the profile a new guid
            profile.Guid = Guid.NewGuid();
            profile.IsSubProfile = true;
            profile.ParentGuid = mainProfile.Guid;

            // set update source so UpdateOrCreateProfile() will (re)create the file
            updateSource = UpdateSource.Creation;
        }
        else if (imported)
        {
            // if imported profile targeted file doesn't exist, use executable as path
            bool pathExist = File.Exists(profile.Path);
            if (!pathExist)
                profile.Path = profile.Executable;
        }

        UpdateOrCreateProfile(profile, updateSource);

        // default specific
        if (profile.Default)
            ApplyProfile(profile, updateSource);
    }

    private List<string> pendingCreation = [];
    private List<string> pendingDeletion = [];

    public void DeleteProfile(Profile profile)
    {
        string profilePath = Path.Combine(ManagerPath, profile.GetFileName());
        pendingDeletion.Add(profilePath);

        if (profiles.ContainsKey(profile.Guid))
        {
            // delete associated subprofiles
            foreach (Profile subprofile in GetSubProfilesFromProfile(profile))
                DeleteProfile(subprofile);

            LogManager.LogInformation("Deleted subprofiles for profile: {0}", profile);

            // Unregister application from HidHide
            HidHide.UnregisterApplication(profile.Path);

            // Remove XInputPlus (extended compatibility)
            XInputPlus.UnregisterApplication(profile);

            _ = profiles.TryRemove(profile.Guid, out Profile removedValue);

            // warn owner
            bool isCurrent = false;

            lock (profileLock)
            {
                isCurrent = profile.Path.Equals(currentProfile?.Path, StringComparison.InvariantCultureIgnoreCase);
            }

            // raise event
            Discarded?.Invoke(profile, isCurrent, GetDefault());

            // raise event(s)
            Deleted?.Invoke(profile);

            // send toast
            // todo: localize me
            ToastManager.SendToast($"{(profile.IsSubProfile ? "Subprofile" : "Profile")} {profile.Name} deleted");

            LogManager.LogInformation($"Deleted {(profile.IsSubProfile ? "subprofile" : "profile")}: {0}", profilePath);

            // restore default profile
            if (isCurrent)
                ApplyProfile(GetDefault());
        }

        FileUtils.FileDelete(profilePath);
    }

    public void SerializeProfile(Profile profile)
    {
        // prepare for writing
        string profilePath = Path.Combine(ManagerPath, profile.GetFileName());
        pendingCreation.Add(profilePath);

        // update profile version to current build
        profile.Version = MainWindow.CurrentVersion;

        string jsonString = JsonConvert.SerializeObject(profile, Formatting.Indented, new JsonSerializerSettings
        {
            TypeNameHandling = TypeNameHandling.All
        });

        try
        {
            // delete old file if fileName has changed
            if (!string.IsNullOrEmpty(profile.FileName) && !profile.FileName.Equals(profile.GetFileName(), StringComparison.InvariantCultureIgnoreCase))
            {
                string oldPath = Path.Combine(ManagerPath, profile.FileName);
                File.Delete(oldPath);
            }

            if (FileUtils.IsFileWritable(profilePath))
            {
                // update profile filename
                profile.FileName = profile.GetFileName();

                File.WriteAllText(profilePath, jsonString);
            }
        }
        catch { }
    }

    private void SanitizeProfile(Profile profile, UpdateSource source = UpdateSource.Background)
    {
        // Decide which profile to run through the sanitizer
        Profile profileToSanitize = profile.IsSubProfile ? GetParent(profile) : profile;

        if (profile.ParentGuid == profile.Guid)
        {
            LogManager.LogError("Profile {0}-{1} is its own parent {1}", profile.Name, profile.Guid);
            profile.ParentGuid = Guid.Empty;
        }

        if (profile.Default && !string.IsNullOrEmpty(profile.Path))
        {
            LogManager.LogError("Profile {0}-{1} is default with not null path {2}", profile.Name, profile.Guid, profile.Path);
            profile.Default = false;
            profile.Path = profile.Executables.First() ?? string.Empty;
        }

        // Manage ErrorCode
        profileToSanitize.ErrorCode = ProfileErrorCode.None;
        if (profileToSanitize.Default)
        {
            profileToSanitize.ErrorCode |= ProfileErrorCode.Default;
        }
        else
        {
            string? processpath = Path.GetDirectoryName(profileToSanitize.Path);
            if (string.IsNullOrEmpty(processpath) || !Directory.Exists(processpath))
            {
                profileToSanitize.ErrorCode |= ProfileErrorCode.MissingPath;
            }
            else
            {
                if (!File.Exists(profileToSanitize.Path))
                    profileToSanitize.ErrorCode |= ProfileErrorCode.MissingExecutable;

                if (!FileUtils.IsDirectoryWritable(processpath))
                    profileToSanitize.ErrorCode |= ProfileErrorCode.MissingPermission;
            }

            // todo: shouldn't we use path ?
            if (ProcessManager.GetProcesses(profile.Executable).Any())
                profile.ErrorCode |= ProfileErrorCode.Running;
        }

        // check if profile power profile was deleted, if so, restore balanced
        for (int idx = 0; idx < 2; idx++)
        {
            Guid powerProfile = profileToSanitize.PowerProfiles[idx];
            if (powerProfile == Guid.Empty)
                continue;

            if (!ManagerFactory.powerProfileManager.Contains(powerProfile))
                profileToSanitize.PowerProfiles[idx] = Guid.Empty;
        }
    }

    public bool IsCurrentProfile(Profile profile)
    {
        lock (profileLock)
        {
            return currentProfile != null
                && profile.Path.Equals(currentProfile.Path, StringComparison.InvariantCultureIgnoreCase);
        }
    }

    public void UpdateOrCreateProfile(Profile profile, UpdateSource source = UpdateSource.Background)
    {
        bool isCurrent = source switch
        {
            UpdateSource.QuickProfilesCreation => true,
            _ => IsCurrentProfile(profile)
        };

        string profileType = profile.IsSubProfile ? "subprofile" : "profile";
        string verb = source == UpdateSource.Serializer ? "Loaded" : "Attempting to update/create";
        LogManager.LogInformation("{0} {1}: {2}", verb, profileType, profile.Name);

        if (source is UpdateSource.Creation or UpdateSource.QuickProfilesCreation)
        {
            // update vars
            profile.DateModified = profile.DateCreated = DateTime.Now;
            if (source is UpdateSource.QuickProfilesCreation)
                profile.LastUsed = profile.DateModified;

            // download arts
            switch(profile.Executable)
            {
                // skip Windows Explorer
                case "explorer.exe":
                    profile.ShowInLibrary = false;
                    break;

                default:
                    ManagerFactory.libraryManager.RefreshProfileArts(profile);
                    break;
            }
        }

        // used to get and store a few previous values
        XInputPlusMethod prevWrapper = XInputPlusMethod.Disabled;
        if (!profile.IsSubProfile && profiles.TryGetValue(profile.Guid, out Profile prevProfile))
        {
            prevWrapper = prevProfile.XInputPlus;
        }

        // update database
        profiles[profile.Guid] = profile;

        // refresh error code
        SanitizeProfile(profile, source);

        // raise event(s)
        Updated?.Invoke(profile, source, isCurrent);

        if (source == UpdateSource.Serializer)
            return;

        // update vars
        profile.DateModified = DateTime.Now;

        // do not update wrapper and cloaking from default profile
        if (!profile.Default)
        {
            // update wrapper
            if (!UpdateProfileWrapper(profile))
            {
                // restore previous XInputPlus mode if failed to update
                profile.XInputPlus = prevWrapper;
                source = UpdateSource.Background;
            }

            // update cloaking
            UpdateProfileCloaking(profile);
        }

        // apply profile (silently)
        if (isCurrent)
        {
            SetFavorite(profile); // if sub profile, set it as favorite for main profile
            ApplyProfile(profile, source);
        }

        // serialize profile
        SerializeProfile(profile);
    }

    public bool UpdateProfileCloaking(Profile profile)
    {
        switch (profile.ErrorCode)
        {
            case ProfileErrorCode.MissingExecutable:
            case ProfileErrorCode.MissingPath:
            case ProfileErrorCode.Default:
                return false;
        }

        switch (profile.Whitelisted)
        {
            case true:
                return HidHide.RegisterApplication(profile.Path);
            default:
            case false:
                return HidHide.UnregisterApplication(profile.Path);
        }
    }

    public bool UpdateProfileWrapper(Profile profile)
    {
        switch (profile.ErrorCode)
        {
            case ProfileErrorCode.MissingPermission:
            case ProfileErrorCode.MissingPath:
            case ProfileErrorCode.Running:
            case ProfileErrorCode.Default:
                return false;
        }

        switch (profile.XInputPlus)
        {
            case XInputPlusMethod.Redirection:
                return XInputPlus.RegisterApplication(profile);
            default:
            case XInputPlusMethod.Disabled:
            case XInputPlusMethod.Injection:
                return XInputPlus.UnregisterApplication(profile);
        }
    }

    private void ControllerManager_ControllerPlugged(IController Controller, bool IsPowerCycling)
    {
        // we're only interest in virtual, XInput controllers
        if (Controller is not XInputController || !Controller.IsVirtual())
            return;

        foreach (var profile in profiles.Values)
            UpdateProfileWrapper(profile);
    }

    #region events

    public event DeletedEventHandler Deleted;
    public delegate void DeletedEventHandler(Profile profile);

    public event UpdatedEventHandler Updated;
    public delegate void UpdatedEventHandler(Profile profile, UpdateSource source, bool isCurrent);

    public event AppliedEventHandler Applied;
    public delegate void AppliedEventHandler(Profile profile, UpdateSource source);

    public event DiscardedEventHandler Discarded;
    public delegate void DiscardedEventHandler(Profile profile, bool swapped, Profile nextProfile);

    #endregion
}