using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Cache;
using System.Reflection;
using HandheldCompanion.Managers;
using HandheldCompanion.Misc;
using HandheldCompanion.Properties;
using HandheldCompanion.Views;
using Inkore.UI.WPF.Modern.Controls;
using Newtonsoft.Json;

namespace HandheldCompanion.Managers;

public class UpdateManager : Manager
{
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

    private readonly Assembly assembly;

    private readonly Version build;
    private DateTime lastchecked;

    private UpdateStatus status;
    private readonly Dictionary<string, UpdateFile> updateFiles = new();
    private string url;
    private readonly WebClient webClient;

    public UpdateManager()
    {
        // check assembly
        assembly = Assembly.GetExecutingAssembly();
        build = assembly.GetName().Version;

        InstallPath = Path.Combine(MainWindow.SettingsPath, "cache");

        // initialize folder
        if (!Directory.Exists(InstallPath))
            Directory.CreateDirectory(InstallPath);

        webClient = new WebClient();
        webClient.CachePolicy = new RequestCachePolicy(RequestCacheLevel.NoCacheNoStore);
        ServicePointManager.Expect100Continue = true;
        webClient.Headers.Add("user-agent", "request");

        webClient.DownloadStringCompleted += WebClient_DownloadStringCompleted;
        webClient.DownloadProgressChanged += WebClient_DownloadProgressChanged;
        webClient.DownloadFileCompleted += WebClient_DownloadFileCompleted;

        SettingsManager.SettingValueChanged += SettingsManager_SettingValueChanged;
    }

    public event UpdatedEventHandler Updated;

    private void SettingsManager_SettingValueChanged(string name, object value)
    {
        switch (name)
        {
            case "UpdateUrl":
                url = Convert.ToString(value);
                break;
        }
    }

    private int GetFileSize(Uri uriPath)
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

    private void WebClient_DownloadFileCompleted(object? sender, AsyncCompletedEventArgs e)
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

    private void WebClient_DownloadProgressChanged(object sender, DownloadProgressChangedEventArgs e)
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

    private void WebClient_DownloadStringCompleted(object sender, DownloadStringCompletedEventArgs e)
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

                _ = Dialog.ShowAsync($"{Resources.SettingsPage_UpdateWarning}",
                    Resources.SettingsPage_UpdateFailedDownload,
                    ContentDialogButton.Primary, string.Empty, $"{Resources.ProfilesPage_OK}");
            }
            else
            {
                _ = Dialog.ShowAsync($"{Resources.SettingsPage_UpdateWarning}",
                    Resources.SettingsPage_UpdateFailedGithub,
                    ContentDialogButton.Primary, string.Empty, $"{Resources.ProfilesPage_OK}");
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

    public void DownloadUpdateFile(UpdateFile update)
    {
        if (webClient.IsBusy)
            return; // lazy

        status = UpdateStatus.Download;
        Updated?.Invoke(status, update, null);

        // download release
        var filename = Path.Combine(InstallPath, update.filename);
        webClient.DownloadFileAsync(update.uri, filename, update.filename);
    }

    private void ParseLatest(string contentsJson)
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

    public override void Start()
    {
        var dateTime = SettingsManager.GetDateTime("UpdateLastChecked");

        lastchecked = dateTime;

        status = UpdateStatus.Initialized;
        Updated?.Invoke(status, null, null);

        base.Start();
    }

    public override void Stop()
    {
        if (!IsInitialized)
            return;

        base.Stop();
    }

    public DateTime GetTime()
    {
        return lastchecked;
    }

    public void UpdateTime()
    {
        lastchecked = DateTime.Now;
        SettingsManager.SetProperty("UpdateLastChecked", lastchecked);
    }

    public void StartProcess()
    {
        // Update UI
        status = UpdateStatus.Checking;
        Updated?.Invoke(status, null, null);

        // download github
        webClient.DownloadStringAsync(new Uri($"{url}/releases/latest"));
    }

    public void InstallUpdate(UpdateFile updateFile)
    {
        var filename = Path.Combine(InstallPath, updateFile.filename);

        if (!File.Exists(filename))
        {
            _ = Dialog.ShowAsync($"{Resources.SettingsPage_UpdateWarning}",
                Resources.SettingsPage_UpdateFailedInstall,
                ContentDialogButton.Primary, string.Empty, $"{Resources.ProfilesPage_OK}");
            return;
        }

        Process.Start(filename);
        Process.GetCurrentProcess().Kill();
    }
}