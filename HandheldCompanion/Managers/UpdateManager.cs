using HandheldCompanion.Misc;
using HandheldCompanion.Properties;
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
    private static DateTime lastchecked;

    private static UpdateStatus status;
    private static readonly Dictionary<string, UpdateFile> updateFiles = [];
    private static string url;
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

        SettingsManager.SettingValueChanged += SettingsManager_SettingValueChanged;
    }

    private static void SettingsManager_SettingValueChanged(string name, object value)
    {
        switch (name)
        {
            case "UpdateUrl":
                url = Convert.ToString(value);
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
        if (status != UpdateStatus.Downloading)
            return;

        var filename = (string)e.UserState;

        if (!updateFiles.ContainsKey(filename))
            return;

        var update = updateFiles[filename];

        status = UpdateStatus.Downloaded;
        Updated?.Invoke(status, update, null);
    }

    private static void WebClient_DownloadProgressChanged(object sender, DownloadProgressChangedEventArgs e)
    {
        if (status != UpdateStatus.Download && status != UpdateStatus.Downloading)
            return;

        var filename = (string)e.UserState;

        if (updateFiles.TryGetValue(filename, out var file))
        {
            status = UpdateStatus.Downloading;
            Updated?.Invoke(status, file, e.ProgressPercentage);
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

            status = UpdateStatus.Failed;
            Updated?.Invoke(status, update, e.Error);
            return;
        }

        switch (status)
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

        status = UpdateStatus.Download;
        Updated?.Invoke(status, update, null);

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
                status = UpdateStatus.Updated;
                Updated?.Invoke(status, null, null);
                return;
            }

            // send changelog
            status = UpdateStatus.Changelog;
            Updated?.Invoke(status, null, latestRelease.body);

            // skip if no assets are currently linked to the release
            if (latestRelease.assets.Count == 0)
            {
                status = UpdateStatus.Updated;
                Updated?.Invoke(status, null, null);
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
                status = UpdateStatus.Failed;
                Updated?.Invoke(status, null, null);
                return;
            }

            status = UpdateStatus.Ready;
            Updated?.Invoke(status, null, updateFiles);
        }
        catch
        {
            // failed to parse Json
            status = UpdateStatus.Failed;
            Updated?.Invoke(status, null, null);
        }
    }

    public static void Start()
    {
        var dateTime = SettingsManager.GetDateTime("UpdateLastChecked");

        lastchecked = dateTime;

        status = UpdateStatus.Initialized;
        Updated?.Invoke(status, null, null);

        IsInitialized = true;
        Initialized?.Invoke();

        LogManager.LogInformation("{0} has started", "UpdateManager");
    }

    public static void Stop()
    {
        if (!IsInitialized)
            return;

        IsInitialized = false;

        SettingsManager.SettingValueChanged -= SettingsManager_SettingValueChanged;

        LogManager.LogInformation("{0} has stopped", "UpdateManager");
    }

    public static DateTime GetTime()
    {
        return lastchecked;
    }

    private static void UpdateTime()
    {
        lastchecked = DateTime.Now;
        SettingsManager.SetProperty("UpdateLastChecked", lastchecked);
    }

    public static void StartProcess()
    {
        // Update UI
        status = UpdateStatus.Checking;
        Updated?.Invoke(status, null, null);

        // download github
        webClient.DownloadStringAsync(new Uri($"{url}/releases/latest"));
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