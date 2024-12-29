using HandheldCompanion.Misc;
using HandheldCompanion.Properties;
using HandheldCompanion.Shared;
using HandheldCompanion.Views;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Cache;
using System.Reflection;
using System.Windows;

namespace HandheldCompanion.Managers;

public static class UpdateManager
{
    public static event UpdatedEventHandler Updated;
    public delegate void UpdatedEventHandler(UpdateStatus status, UpdateFile? update, object? value);

    public enum UpdateStatus
    {
        Initialized,
        Updated,
        Checking,
        Changelog,
        Ready,
        Download,
        Downloading,
        Downloaded,
        Failed
    }

    private static readonly Assembly assembly;
    private static readonly Version build;

    private static DateTime lastCheck;
    private static UpdateStatus updateStatus;
    private static readonly Dictionary<string, UpdateFile> updateFiles = [];
    private static string updateUrl;

    private static readonly WebClient webClient;
    private static readonly string InstallPath;

    private static bool IsInitialized;

    public static event InitializedEventHandler Initialized;
    public delegate void InitializedEventHandler();

    static UpdateManager()
    {
        // check assembly
        assembly = Assembly.GetExecutingAssembly();
        build = assembly.GetName().Version;

        InstallPath = Path.Combine(MainWindow.SettingsPath, "cache");

        // initialize folder
        if (!Directory.Exists(InstallPath))
            Directory.CreateDirectory(InstallPath);

        webClient = new WebClient
        {
            CachePolicy = new RequestCachePolicy(RequestCacheLevel.NoCacheNoStore)
        };
        ServicePointManager.Expect100Continue = true;
        webClient.Headers.Add("user-agent", "request");

        webClient.DownloadStringCompleted += WebClient_DownloadStringCompleted;
        webClient.DownloadProgressChanged += WebClient_DownloadProgressChanged;
        webClient.DownloadFileCompleted += WebClient_DownloadFileCompleted;
    }

    public static void Start()
    {
        if (IsInitialized)
            return;

        DateTime dateTime = SettingsManager.GetDateTime("UpdateLastChecked");

        lastCheck = dateTime;

        updateStatus = UpdateStatus.Initialized;
        Updated?.Invoke(updateStatus, null, null);

        // manage events
        SettingsManager.SettingValueChanged += SettingsManager_SettingValueChanged;

        // raise events
        if (SettingsManager.IsInitialized || SettingsManager.IsInitializing)
        {
            SettingsManager_SettingValueChanged("UpdateUrl", SettingsManager.GetString("UpdateUrl"), false);
        }

        IsInitialized = true;
        Initialized?.Invoke();

        LogManager.LogInformation("{0} has started", "UpdateManager");
    }

    public static void Stop()
    {
        if (!IsInitialized)
            return;

        // manage events
        SettingsManager.SettingValueChanged -= SettingsManager_SettingValueChanged;

        IsInitialized = false;

        LogManager.LogInformation("{0} has stopped", "UpdateManager");
    }

    private static void SettingsManager_SettingValueChanged(string name, object value, bool temporary)
    {
        switch (name)
        {
            case "UpdateUrl":
                updateUrl = Convert.ToString(value);
                break;
        }
    }

    private static int GetFileSize(Uri uriPath)
    {
        try
        {
            var webRequest = WebRequest.Create(uriPath);
            webRequest.Method = "HEAD";

            using (var webResponse = webRequest.GetResponse())
            {
                var fileSize = webResponse.Headers.Get("Content-Length");
                return Convert.ToInt32(fileSize);
            }
        }
        catch
        {
            return 0;
        }
    }

    private static void WebClient_DownloadFileCompleted(object? sender, AsyncCompletedEventArgs e)
    {
        if (updateStatus != UpdateStatus.Downloading)
            return;

        var filename = (string)e.UserState;

        if (!updateFiles.ContainsKey(filename))
            return;

        var update = updateFiles[filename];

        updateStatus = UpdateStatus.Downloaded;
        Updated?.Invoke(updateStatus, update, null);
    }

    private static void WebClient_DownloadProgressChanged(object sender, DownloadProgressChangedEventArgs e)
    {
        if (updateStatus != UpdateStatus.Download && updateStatus != UpdateStatus.Downloading)
            return;

        var filename = (string)e.UserState;

        if (updateFiles.TryGetValue(filename, out var file))
        {
            updateStatus = UpdateStatus.Downloading;
            Updated?.Invoke(updateStatus, file, e.ProgressPercentage);
        }
    }

    private static void WebClient_DownloadStringCompleted(object sender, DownloadStringCompletedEventArgs e)
    {
        // something went wrong with the connection
        if (e.Error is not null)
        {
            UpdateFile update = null;

            if (e.UserState is not null)
            {
                var filename = (string)e.UserState;
                if (updateFiles.TryGetValue(filename, out var file))
                    update = file;

                // UI thread
                Application.Current.Dispatcher.Invoke(() =>
                {
                    _ = new Dialog(MainWindow.GetCurrent())
                    {
                        Title = Resources.SettingsPage_UpdateWarning,
                        Content = Resources.SettingsPage_UpdateFailedDownload,
                        PrimaryButtonText = Resources.ProfilesPage_OK
                    }.ShowAsync();
                });
            }
            else
            {
                // UI thread
                Application.Current.Dispatcher.Invoke(() =>
                {
                    _ = new Dialog(MainWindow.GetCurrent())
                    {
                        Title = Resources.SettingsPage_UpdateWarning,
                        Content = Resources.SettingsPage_UpdateFailedGithub,
                        PrimaryButtonText = Resources.ProfilesPage_OK
                    }.ShowAsync();
                });
            }

            updateStatus = UpdateStatus.Failed;
            Updated?.Invoke(updateStatus, update, e.Error);
            return;
        }

        switch (updateStatus)
        {
            case UpdateStatus.Checking:
                ParseLatest(e.Result);
                break;
        }
    }

    public static void DownloadUpdateFile(UpdateFile update)
    {
        if (webClient.IsBusy)
            return; // lazy

        updateStatus = UpdateStatus.Download;
        Updated?.Invoke(updateStatus, update, null);

        // download release
        var filename = Path.Combine(InstallPath, update.filename);
        webClient.DownloadFileAsync(update.uri, filename, update.filename);
    }

    private static void ParseLatest(string contentsJson)
    {
        try
        {
            var latestRelease = JsonConvert.DeserializeObject<GitRelease>(contentsJson);

            // get latest build version
            var latestBuild = new Version(latestRelease.tag_name);

            // update latest check time
            UpdateTime();

            // skip if user is already running latest build
            if (latestBuild <= build)
            {
                updateStatus = UpdateStatus.Updated;
                Updated?.Invoke(updateStatus, null, null);
                return;
            }

            // send changelog
            updateStatus = UpdateStatus.Changelog;
            Updated?.Invoke(updateStatus, null, latestRelease.body);

            // skip if no assets are currently linked to the release
            if (latestRelease.assets.Count == 0)
            {
                updateStatus = UpdateStatus.Updated;
                Updated?.Invoke(updateStatus, null, null);
                return;
            }

            foreach (var asset in latestRelease.assets)
            {
                var uri = new Uri(asset.browser_download_url);
                var update = new UpdateFile
                {
                    idx = (short)asset.id,
                    filename = asset.name,
                    uri = uri,
                    filesize = GetFileSize(uri),
                    debug = asset.name.Contains("Debug", StringComparison.InvariantCultureIgnoreCase)
                };

                // making sure there was no corruption
                if (update.filesize == asset.size)
                    updateFiles.Add(update.filename, update);
            }

            // skip if we failed to parse updates
            if (updateFiles.Count == 0)
            {
                updateStatus = UpdateStatus.Failed;
                Updated?.Invoke(updateStatus, null, null);
                return;
            }

            updateStatus = UpdateStatus.Ready;
            Updated?.Invoke(updateStatus, null, updateFiles);
        }
        catch
        {
            // failed to parse Json
            updateStatus = UpdateStatus.Failed;
            Updated?.Invoke(updateStatus, null, null);
        }
    }

    public static DateTime GetTime()
    {
        return lastCheck;
    }

    private static void UpdateTime()
    {
        lastCheck = DateTime.Now;
        SettingsManager.SetProperty("UpdateLastChecked", lastCheck);
    }

    public static void StartProcess()
    {
        // Update UI
        updateStatus = UpdateStatus.Checking;
        Updated?.Invoke(updateStatus, null, null);

        // download github
        webClient.DownloadStringAsync(new Uri($"{updateUrl}/releases/latest"));
    }

    public static void InstallUpdate(UpdateFile updateFile)
    {
        var filename = Path.Combine(InstallPath, updateFile.filename);

        if (!File.Exists(filename))
        {
            // UI thread
            Application.Current.Dispatcher.Invoke(() =>
            {
                _ = new Dialog(MainWindow.GetCurrent())
                {
                    Title = Resources.SettingsPage_UpdateWarning,
                    Content = Resources.SettingsPage_UpdateFailedInstall,
                    PrimaryButtonText = Resources.ProfilesPage_OK
                }.ShowAsync();
            });
            return;
        }

        Process.Start(filename);
        Process.GetCurrentProcess().Kill();
    }
}