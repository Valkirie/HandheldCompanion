using HandheldCompanion.Notifications;
using HandheldCompanion.Shared;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using System.Timers;

namespace HandheldCompanion.Managers
{
    public static class UpdateManager
    {
        public static event UpdatedEventHandler? Updated;
        public delegate void UpdatedEventHandler(UpdateStatus status, UpdateFile? update, object? value);

        public enum UpdateStatus
        {
            Initialized,
            Updated,
            Checking,
            Changelog,
            Ready,
            ControllerDbReady,
            Download,
            Downloading,
            Downloaded,
            Failed
        }

        private static DateTime lastCheck;
        private static UpdateStatus updateStatus;
        private static readonly Dictionary<string, UpdateFile> updateFiles = new();

        private static string updateUrl = string.Empty;
        private static readonly HttpClient httpClient;
        private static readonly string InstallPath;

        private static Timer autoTimer = new(TimeSpan.FromMinutes(10)) { AutoReset = true };

        public static DateTime GetTime() => lastCheck;

        private static bool IsInitialized;

        public static event InitializedEventHandler? Initialized;
        public delegate void InitializedEventHandler();

        static UpdateManager()
        {
            // prepare cache folder
            InstallPath = Path.Combine(App.SettingsPath, "cache");
            if (!Directory.Exists(InstallPath))
                Directory.CreateDirectory(InstallPath);

            // configure HttpClient with GitHub headers (prevents 403 on reuse)
            var handler = new HttpClientHandler
            {
                AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
            };
            httpClient = new HttpClient(handler);
            httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("HandheldCompanion/1.0");
            httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github.v3+json"));

            autoTimer.Elapsed += AutoTimer_Elapsed;
        }

        private static void AutoTimer_Elapsed(object? sender, ElapsedEventArgs e)
        {
            _ = StartProcess(true);
        }

        public static void Start()
        {
            if (IsInitialized)
                return;

            lastCheck = ManagerFactory.settingsManager.GetDateTime("UpdateLastChecked");

            autoTimer.Start();

            updateStatus = UpdateStatus.Initialized;
            Updated?.Invoke(updateStatus, null, null);

            // raise events
            switch (ManagerFactory.settingsManager.Status)
            {
                default:
                case ManagerStatus.Initializing:
                    ManagerFactory.settingsManager.Initialized += SettingsManager_Initialized;
                    break;
                case ManagerStatus.Initialized:
                    QuerySettings();
                    break;
            }

            IsInitialized = true;
            Initialized?.Invoke();

            // force trigger an update check on HC startup
            _ = StartProcess(true);

            LogManager.LogInformation("{0} has started", "UpdateManager");
        }

        public static void Stop()
        {
            if (!IsInitialized)
                return;

            autoTimer.Stop();
            autoTimer.Dispose();

            ManagerFactory.settingsManager.SettingValueChanged -= SettingsManager_SettingValueChanged;
            ManagerFactory.settingsManager.Initialized -= SettingsManager_Initialized;

            IsInitialized = false;
            LogManager.LogInformation("{0} has stopped", "UpdateManager");
        }

        private static void SettingsManager_Initialized() => QuerySettings();

        private static void QuerySettings()
        {
            // manage events
            ManagerFactory.settingsManager.SettingValueChanged += SettingsManager_SettingValueChanged;

            SettingsManager_SettingValueChanged("UpdateUrl", ManagerFactory.settingsManager.GetString("UpdateUrl"), false, false);
        }

        private static void SettingsManager_SettingValueChanged(string name, object? value, bool temporary, bool initializing)
        {
            switch (name)
            {
                case "UpdateUrl":
                    updateUrl = Convert.ToString(value) ?? string.Empty;
                    break;
            }
        }

        private static int GetFileSize(Uri uriPath)
        {
            try
            {
                var webRequest = WebRequest.Create(uriPath);
                webRequest.Method = "HEAD";

                using var webResponse = webRequest.GetResponse();
                var fileSize = webResponse.Headers.Get("Content-Length");
                return Convert.ToInt32(fileSize);
            }
            catch
            {
                return 0;
            }
        }

        private static void UpdateTime()
        {
            lastCheck = DateTime.Now;
            ManagerFactory.settingsManager.SetProperty("UpdateLastChecked", lastCheck);
        }

        private const string GameControllerDbUrl = "https://raw.githubusercontent.com/mdqinc/SDL_GameControllerDB/refs/heads/master/gamecontrollerdb.txt";
        private static UpdateFile? controllerDbFile;

        private static uint ComputeCrc32(string filePath)
        {
            const uint poly = 0xEDB88320u;
            uint crc = 0xFFFFFFFFu;
            foreach (byte b in File.ReadAllBytes(filePath))
            {
                crc ^= b;
                for (int i = 0; i < 8; i++)
                    crc = (crc & 1) != 0 ? (crc >> 1) ^ poly : crc >> 1;
            }
            return crc ^ 0xFFFFFFFFu;
        }

        private static async Task CheckGameControllerDb(bool background)
        {
            try
            {
                // Download the remote file to the cache
                var cachedPath = Path.Combine(InstallPath, "gamecontrollerdb.txt");
                byte[] data = await httpClient.GetByteArrayAsync(GameControllerDbUrl).ConfigureAwait(false);
                await File.WriteAllBytesAsync(cachedPath, data).ConfigureAwait(false);

                // Compare CRC32 against the installed copy — any difference means the remote was updated
                if (File.Exists(App.GameControllerDbPath) && ComputeCrc32(cachedPath) == ComputeCrc32(App.GameControllerDbPath))
                {
                    LogManager.LogInformation("gamecontrollerdb.txt is already up to date");
                    return;
                }

                controllerDbFile = new UpdateFile
                {
                    filename = "gamecontrollerdb.txt",
                    uri = new Uri(GameControllerDbUrl),
                    filesize = data.Length,
                    isGameControllerDb = true
                };

                ControllerDbAvailable.Message = "A newer SDL Game Controller database is available";
                ManagerFactory.notificationManager.Add(ControllerDbAvailable);
                ToastManager.SendToast(ControllerDbAvailable.Action, ControllerDbAvailable.Message);

                // File is already in the cache — surface it as Downloaded so the UI shows "Install Now"
                Updated?.Invoke(UpdateStatus.ControllerDbReady, controllerDbFile, null);
                Updated?.Invoke(UpdateStatus.Downloaded, controllerDbFile, null);
            }
            catch (Exception ex)
            {
                LogManager.LogWarning("Failed to check gamecontrollerdb.txt: {0}", ex.Message);
            }
        }

        private static async Task CheckGithubUpdate(bool background)
        {
            try
            {
                var resp = await httpClient.GetAsync($"{updateUrl}/releases/latest");
                resp.EnsureSuccessStatusCode();
                var contents = await resp.Content.ReadAsStringAsync();

                // parse result
                ParseLatest(contents);
            }
            catch (Exception)
            {
                if (background)
                    return;

                updateStatus = UpdateStatus.Failed;
                Updated?.Invoke(updateStatus, null, null);
            }
        }

        public static async Task StartProcess(bool background)
        {
            updateStatus = UpdateStatus.Checking;
            Updated?.Invoke(updateStatus, null, background);

            await Task.WhenAll(
                CheckGameControllerDb(background),
                CheckGithubUpdate(background)
            );
        }

        public static async Task DownloadUpdateFile(UpdateFile update)
        {
            if (updateStatus == UpdateStatus.Downloading)
                return;

            updateStatus = UpdateStatus.Download;
            Updated?.Invoke(updateStatus, update, null);

            var destPath = Path.Combine(InstallPath, update.filename);
            try
            {
                using var resp = await httpClient.GetAsync(update.uri, HttpCompletionOption.ResponseHeadersRead);
                resp.EnsureSuccessStatusCode();

                long totalBytes = update.filesize > 0
                    ? update.filesize
                    : resp.Content.Headers.ContentLength ?? -1;

                using var sourceStream = await resp.Content.ReadAsStreamAsync();
                using var destStream = new FileStream(destPath, FileMode.Create, FileAccess.Write, FileShare.None);
                var buffer = new byte[81920];
                long bytesReadSoFar = 0;
                int read;
                while ((read = await sourceStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                {
                    await destStream.WriteAsync(buffer, 0, read);
                    bytesReadSoFar += read;

                    if (totalBytes > 0)
                    {
                        int percent = (int)(bytesReadSoFar * 100L / totalBytes);
                        updateStatus = UpdateStatus.Downloading;
                        Updated?.Invoke(updateStatus, update, percent);
                    }
                }

                updateStatus = UpdateStatus.Downloaded;
                Updated?.Invoke(updateStatus, update, null);
            }
            catch (Exception)
            {
                updateStatus = UpdateStatus.Failed;
                Updated?.Invoke(updateStatus, update, null);
            }
        }

        private static Notification UpdateAvailable = new("Update Manager", "An update is ready for download") { IsInternal = true, IsIndeterminate = true };
        private static Notification ControllerDbAvailable = new("Update Manager", "A newer SDL Game Controller database is available") { IsInternal = true, IsIndeterminate = true };
        private static void ParseLatest(string contentsJson)
        {
            try
            {
                GitRelease latestRelease = JsonConvert.DeserializeObject<GitRelease>(contentsJson)!;
                Version latestBuild = new Version(latestRelease.tag_name);

                UpdateTime();

                if (latestBuild <= App.CurrentVersion)
                {
                    updateStatus = UpdateStatus.Updated;
                    Updated?.Invoke(updateStatus, null, null);
                    return;
                }

                // update message
                UpdateAvailable.Message = $"Version {latestBuild.ToString()} is ready for download";

                ManagerFactory.notificationManager.Add(UpdateAvailable);
                ToastManager.SendToast(UpdateAvailable.Action, UpdateAvailable.Message);

                // send changelog
                updateStatus = UpdateStatus.Changelog;
                Updated?.Invoke(updateStatus, null, latestRelease.body);

                if (latestRelease.assets.Count == 0)
                {
                    updateStatus = UpdateStatus.Updated;
                    Updated?.Invoke(updateStatus, null, null);
                    return;
                }

                updateFiles.Clear();
                foreach (var asset in latestRelease.assets)
                {
                    var uri = new Uri(asset.browser_download_url);
                    var file = new UpdateFile
                    {
                        idx = (short)asset.id,
                        filename = asset.name,
                        uri = uri,
                        filesize = GetFileSize(uri),
                        debug = asset.name.Contains("Debug", StringComparison.InvariantCultureIgnoreCase)
                    };

                    if (file.filesize == asset.size)
                        updateFiles[file.filename] = file;
                }

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
                updateStatus = UpdateStatus.Failed;
                Updated?.Invoke(updateStatus, null, null);
            }
        }

        public static bool InstallUpdate(UpdateFile updateFile)
        {
            if (updateFile.isGameControllerDb)
            {
                var cached = Path.Combine(InstallPath, updateFile.filename);
                if (!File.Exists(cached))
                    return false;

                try
                {
                    File.Copy(cached, App.GameControllerDbPath, overwrite: true);

                    LogManager.LogInformation("gamecontrollerdb.txt installed successfully");
                    ControllerManager.LoadGamepadMappings();
                    return true;
                }
                catch (Exception ex)
                {
                    LogManager.LogWarning("Failed to install gamecontrollerdb.txt: {0}", ex.Message);
                    return false;
                }
            }

            var filename = Path.Combine(InstallPath, updateFile.filename);

            if (!File.Exists(filename))
                return false;

            Process.Start(filename);
            Process.GetCurrentProcess().Kill();
            return true;
        }
    }
}