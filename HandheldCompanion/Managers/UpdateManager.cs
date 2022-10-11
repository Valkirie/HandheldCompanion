using ControllerCommon.Managers;
using HandheldCompanion.Managers.Classes;
using ModernWpf.Controls;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Cache;
using System.Reflection;
using static System.Windows.Forms.VisualStyles.VisualStyleElement;

namespace HandheldCompanion.Managers
{
    public class UpdateManager : Manager
    {
        private DateTime lastchecked;
        private Assembly assembly;
        private WebClient webClient;
        private Random random = new();

        private Version build;
        private Dictionary<string, UpdateFile> updateFiles = new();

        private UpdateStatus status;
        private string path;

        public event UpdatedEventHandler Updated;
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

        public UpdateManager() : base()
        {
            // check assembly
            assembly = Assembly.GetExecutingAssembly();
            build = assembly.GetName().Version;

            path = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "HandheldCompanion", "cache");

            // initialize folder
            if (!Directory.Exists(path))
                Directory.CreateDirectory(path);

            webClient = new WebClient();
            webClient.CachePolicy = new RequestCachePolicy(RequestCacheLevel.NoCacheNoStore);
            ServicePointManager.Expect100Continue = true;
            webClient.Headers.Add("user-agent", "request");

            webClient.DownloadStringCompleted += WebClient_DownloadStringCompleted;
            webClient.DownloadProgressChanged += WebClient_DownloadProgressChanged;
            webClient.DownloadFileCompleted += WebClient_DownloadFileCompleted;
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
            catch (Exception) { return 0; }
        }

        private void WebClient_DownloadFileCompleted(object? sender, System.ComponentModel.AsyncCompletedEventArgs e)
        {
            if (status != UpdateStatus.Downloading)
                return;

            var filename = (string)e.UserState;

            if (!updateFiles.ContainsKey(filename))
                return;

            UpdateFile update = updateFiles[filename];

            status = UpdateStatus.Downloaded;
            Updated?.Invoke(status, update, null);
        }

        private void WebClient_DownloadProgressChanged(object sender, DownloadProgressChangedEventArgs e)
        {
            if (status != UpdateStatus.Download && status != UpdateStatus.Downloading)
                return;

            var filename = (string)e.UserState;

            if (updateFiles.ContainsKey(filename))
            {
                UpdateFile update = updateFiles[filename];

                status = UpdateStatus.Downloading;
                Updated?.Invoke(status, update, e.ProgressPercentage);
            }
        }

        private void WebClient_DownloadStringCompleted(object sender, DownloadStringCompletedEventArgs e)
        {
            // something went wrong with the connection
            if (e.Error != null)
            {
                UpdateFile update = null;

                if (e.UserState != null)
                {
                    var filename = (string)e.UserState;
                    if (updateFiles.ContainsKey(filename))
                        update = updateFiles[filename];

                    _ = Dialog.ShowAsync($"{Properties.Resources.SettingsPage_UpdateWarning}",
                        Properties.Resources.SettingsPage_UpdateFailedDownload,
                        ContentDialogButton.Primary, String.Empty, $"{Properties.Resources.ProfilesPage_OK}");
                }
                else
                {
                    _ = Dialog.ShowAsync($"{Properties.Resources.SettingsPage_UpdateWarning}",
                        Properties.Resources.SettingsPage_UpdateFailedGithub,
                        ContentDialogButton.Primary, String.Empty, $"{Properties.Resources.ProfilesPage_OK}");
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
            string filename = System.IO.Path.Combine(path, update.filename);
            webClient.DownloadFileAsync(update.uri, filename, update.filename);
        }

        private void ParseLatest(string contentsJson)
        {
            try
            {
                GitRelease latestRelease = JsonConvert.DeserializeObject<GitRelease>(contentsJson);

                // get latest build version
                Version latestBuild = new Version(latestRelease.tag_name);

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

                foreach (Asset asset in latestRelease.assets)
                {
                    Uri uri = new Uri(asset.browser_download_url);
                    UpdateFile update = new UpdateFile()
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
            catch (Exception)
            {
                // failed to parse Json
                status = UpdateStatus.Failed;
                Updated?.Invoke(status, null, null);
                return;
            }
        }

        public override void Start()
        {
            DateTime dateTime = SettingsManager.GetDateTime("UpdateLastChecked");

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
            webClient.DownloadStringAsync(new Uri("https://api.github.com/repos/Valkirie/ControllerService/releases/latest"));
        }

        public void InstallUpdate(UpdateFile updateFile)
        {
            string filename = System.IO.Path.Combine(path, updateFile.filename);

            if (!File.Exists(filename))
            {
                _ = Dialog.ShowAsync($"{Properties.Resources.SettingsPage_UpdateWarning}",
                    Properties.Resources.SettingsPage_UpdateFailedInstall,
                    ContentDialogButton.Primary, String.Empty, $"{Properties.Resources.ProfilesPage_OK}");
                return;
            }

            Process.Start(filename);
            Process.GetCurrentProcess().Kill();
        }
    }
}
