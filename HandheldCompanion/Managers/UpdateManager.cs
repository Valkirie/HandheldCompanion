using ControllerCommon.Managers;
using ControllerCommon.Utils;
using HandheldCompanion.Views;
using ModernWpf.Controls;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Cache;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Xml;

namespace HandheldCompanion.Managers
{
    public enum UpdateStatus
    {
        Initialized,
        Updated,
        CheckingATOM,
        CheckingMETA,
        Ready,
        Download,
        Downloading,
        Downloaded,
        Failed
    }

    public class UpdateFile
    {
        public short idx;
        public Uri uri;
        public double filesize = 0.0d;
        public string filename;

        // UI vars
        public Border updateBorder;
        public Grid updateGrid;
        public TextBlock updateFilename;
        public TextBlock updatePercentage;
        public Button updateDownload;
        public Button updateInstall;

        public Border Draw()
        {
            updateBorder = new Border()
            {
                Padding = new Thickness(20, 12, 12, 12),
                Background = (Brush)Application.Current.Resources["SystemControlBackgroundChromeMediumLowBrush"],
                Tag = filename
            };

            // Create Grid
            updateGrid = new();

            // Define the Columns
            ColumnDefinition colDef1 = new ColumnDefinition()
            {
                Width = new GridLength(5, GridUnitType.Star),
                MinWidth = 200
            };
            updateGrid.ColumnDefinitions.Add(colDef1);

            ColumnDefinition colDef2 = new ColumnDefinition()
            {
                Width = new GridLength(3, GridUnitType.Star),
                MinWidth = 120
            };
            updateGrid.ColumnDefinitions.Add(colDef2);

            // Create TextBlock
            updateFilename = new TextBlock()
            {
                FontSize = 14,
                Text = filename,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(updateFilename, 0);
            updateGrid.Children.Add(updateFilename);

            // Create TextBlock
            updatePercentage = new TextBlock()
            {
                FontSize = 14,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Right,
                Visibility = Visibility.Collapsed
            };
            Grid.SetColumn(updatePercentage, 1);
            updateGrid.Children.Add(updatePercentage);

            // Create Download Button
            updateDownload = new Button()
            {
                FontSize = 14,
                Content = Properties.Resources.SettingsPage_Download,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Right
            };

            Grid.SetColumn(updateDownload, 1);
            updateGrid.Children.Add(updateDownload);

            // Create Install Button
            updateInstall = new Button()
            {
                FontSize = 14,
                Content = Properties.Resources.SettingsPage_InstallNow,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Right,
                Visibility = Visibility.Collapsed
            };

            Grid.SetColumn(updateInstall, 1);
            updateGrid.Children.Add(updateInstall);

            updateBorder.Child = updateGrid;

            return updateBorder;
        }
    }

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
        public delegate void UpdatedEventHandler(UpdateStatus status, UpdateFile update, object value);

        public UpdateManager() : base()
        {
            // check assembly
            assembly = Assembly.GetExecutingAssembly();
            build = assembly.GetName().Version;

            path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "HandheldCompanion", "cache");

            // initialize folder
            if (!Directory.Exists(path))
                Directory.CreateDirectory(path);

            webClient = new WebClient();
            webClient.CachePolicy = new RequestCachePolicy(RequestCacheLevel.NoCacheNoStore);

            webClient.DownloadStringCompleted += WebClient_DownloadStringCompleted;
            webClient.DownloadProgressChanged += WebClient_DownloadProgressChanged;
            webClient.DownloadFileCompleted += WebClient_DownloadFileCompleted;
        }

        private double GetFileSize(Uri uriPath)
        {
            try
            {
                var webRequest = WebRequest.Create(uriPath);
                webRequest.Method = "HEAD";

                using (var webResponse = webRequest.GetResponse())
                {
                    var fileSize = webResponse.Headers.Get("Content-Length");
                    return Math.Round(Convert.ToDouble(fileSize) / 1024.0 / 1024.0, 2); // MB
                }
            }
            catch (Exception ex) { return 0.0d; }
        }

        private void WebClient_DownloadFileCompleted(object? sender, System.ComponentModel.AsyncCompletedEventArgs e)
        {
            if (e.UserState is null)
                return;

            var filename = (string)e.UserState;

            if (!updateFiles.ContainsKey(filename))
                return;

            UpdateFile update = updateFiles[filename];
            Updated?.Invoke(UpdateStatus.Downloaded, update, null);
        }

        private void WebClient_DownloadProgressChanged(object sender, DownloadProgressChangedEventArgs e)
        {
            if (e.UserState is null)
                return;

            var filename = (string)e.UserState;

            if (updateFiles.ContainsKey(filename))
            {
                UpdateFile update = updateFiles[filename];
                Updated?.Invoke(UpdateStatus.Downloading, update, e.ProgressPercentage);
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
                        ContentDialogButton.Primary, null, $"{Properties.Resources.ProfilesPage_OK}");
                }
                else
                {
                    _ = Dialog.ShowAsync($"{Properties.Resources.SettingsPage_UpdateWarning}",
                        Properties.Resources.SettingsPage_UpdateFailedGithub,
                        ContentDialogButton.Primary, null, $"{Properties.Resources.ProfilesPage_OK}");
                }

                Updated?.Invoke(UpdateStatus.Failed, update, e.Error);
                return;
            }

            switch (status)
            {
                case UpdateStatus.CheckingATOM:
                    ParseATOM(e.Result);
                    break;
                case UpdateStatus.CheckingMETA:
                    ParseMETA(e.Result);
                    break;
            }
        }

        private short resultIdx;
        private void ParseMETA(string Result)
        {
            try
            {
                resultIdx = 0;
                updateFiles.Clear();

                string assets = CommonUtils.Between(Result, "Assets</h3>", "</details>");
                string asset = CommonUtils.Between(assets, "<a", "</a>", true);

                while (!string.IsNullOrEmpty(asset))
                {
                    Uri href = new Uri($"https://github.com{CommonUtils.Between(asset, "href=\"", "\"")}");

                    UpdateFile update = new UpdateFile()
                    {
                        idx = resultIdx,
                        filename = Path.GetFileName(href.LocalPath),
                        uri = href,
                        filesize = GetFileSize(href)
                    };

                    if (update.filesize != 0)
                        updateFiles.Add(update.filename, update);

                    assets = assets.Replace(asset, null);

                    // get next iteration
                    asset = CommonUtils.Between(assets, "<a", "</a>", true);
                    resultIdx++;
                }

                // asset_size = double.Parse(CommonUtils.Between(Result, "asset-size-label\">", " MB</span>"), CultureInfo.InvariantCulture);
            }
            catch (Exception) { }

            if (updateFiles.Count == 0)
            {
                Updated?.Invoke(UpdateStatus.Failed, null, null);
                return;
            }

            Updated?.Invoke(UpdateStatus.Ready, null, updateFiles);
        }

        public void DownloadUpdateFile(UpdateFile update)
        {
            if (webClient.IsBusy)
                return; // lazy

            // download release
            string filename = Path.Combine(path, update.filename);
            webClient.DownloadFileAsync(update.uri, filename, update.filename);
            Updated?.Invoke(UpdateStatus.Download, update, null);
        }

        private void ParseATOM(string Result)
        {
            // pull build from github
            XmlDocument doc = new XmlDocument();
            doc.LoadXml(Result);

            XmlNodeList entries = doc.GetElementsByTagName("entry");
            var entry = entries[0];

            Version latestBuild = new Version(0, 0, 0, 0);
            Uri latestHref = null;

            foreach (XmlNode child in entry.ChildNodes)
            {
                switch (child.Name)
                {
                    case "id":
                        {
                            string innerText = child.InnerText;
                            int idx = innerText.LastIndexOf("-") + 1;
                            int len = innerText.Length;

                            latestBuild = Version.Parse(innerText.Substring(idx, len - idx));
                        }
                        break;

                    case "link":
                        {
                            foreach (XmlAttribute attribute in child.Attributes)
                            {
                                switch (attribute.Name)
                                {
                                    case "href":
                                        latestHref = new Uri(attribute.Value);
                                        break;
                                }
                                continue;
                            }
                        }
                        break;
                }
            }

            UpdateTime();

            if (latestBuild <= build)
            {
                Updated?.Invoke(UpdateStatus.Updated, null, null);
                return;
            }

            if (latestHref == null)
            {
                Updated?.Invoke(UpdateStatus.Failed, null, null);
                return;
            }

            status = UpdateStatus.CheckingMETA;
            webClient.DownloadStringAsync(latestHref);
        }

        public override void Start()
        {
            DateTime dateTime = Convert.ToDateTime(MainWindow.settingsManager.GetProperty("UpdateLastChecked"));

            lastchecked = dateTime;
            Updated?.Invoke(UpdateStatus.Initialized, null, null);

            base.Start();
        }

        public DateTime GetTime()
        {
            return lastchecked;
        }

        public void UpdateTime()
        {
            lastchecked = DateTime.Now;
            MainWindow.settingsManager.SetProperty("UpdateLastChecked", lastchecked);
        }

        public void StartProcess()
        {
            // Update UI
            status = UpdateStatus.CheckingATOM;
            Updated?.Invoke(status, null, null);

            // download github
            string restUrl = $"https://github.com/Valkirie/ControllerService/releases.atom?{random.Next()}";
            webClient.DownloadStringAsync(new Uri(restUrl));
        }

        public void InstallUpdate(UpdateFile updateFile)
        {
            string filename = Path.Combine(path, updateFile.filename);

            if (!File.Exists(filename))
            {
                Dialog.ShowAsync($"{Properties.Resources.SettingsPage_UpdateWarning}",
                    Properties.Resources.SettingsPage_UpdateFailedInstall,
                    ContentDialogButton.Primary, null, $"{Properties.Resources.ProfilesPage_OK}");
                return;
            }

            Process.Start(filename);
            Process.GetCurrentProcess().Kill();
        }
    }
}
