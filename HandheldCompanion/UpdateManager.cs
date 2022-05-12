using ControllerCommon.Utils;
using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Cache;
using System.Reflection;
using System.Xml;

namespace HandheldCompanion
{
    public enum UpdateStatus
    {
        Initialized,
        Updated,
        CheckingATOM,
        CheckingMETA,
        Display,
        Ready,
        Downloading,
        Downloaded,
        Failed
    }

    public class UpdateFile
    {
        public Version version;
        public Uri meta;
        public Uri uri;
        public double filesize = 0.0d;
        public string filename;
    }

    public class UpdateManager
    {
        private DateTime lastchecked;
        private Assembly assembly;
        private WebClient webClient;
        private Random random = new();

        private Version build;
        private UpdateFile updateFile;

        private UpdateStatus status;

        public event UpdatedEventHandler Updated;
        public delegate void UpdatedEventHandler(UpdateStatus status, object value);

        public UpdateManager()
        {
            // check assembly
            assembly = Assembly.GetExecutingAssembly();
            build = assembly.GetName().Version;

            webClient = new WebClient();
            webClient.CachePolicy = new RequestCachePolicy(System.Net.Cache.RequestCacheLevel.NoCacheNoStore);

            webClient.DownloadStringCompleted += WebClient_DownloadStringCompleted;
            webClient.DownloadProgressChanged += WebClient_DownloadProgressChanged;
            webClient.DownloadFileCompleted += WebClient_DownloadFileCompleted;
        }

        private double GetFileSize(Uri uriPath)
        {
            try
            {
                var webRequest = HttpWebRequest.Create(uriPath);
                webRequest.Method = "HEAD";

                using (var webResponse = webRequest.GetResponse())
                {
                    var fileSize = webResponse.Headers.Get("Content-Length");
                    return Math.Round(Convert.ToDouble(fileSize) / 1024.0 / 1024.0, 2); // MB
                }
            }catch (Exception ex) { return 0.0d; }
        }

        private void WebClient_DownloadFileCompleted(object? sender, System.ComponentModel.AsyncCompletedEventArgs e)
        {
            switch (status)
            {
                case UpdateStatus.Downloading:
                    status = UpdateStatus.Downloaded;
                    Updated?.Invoke(status, updateFile);
                    break;
            }
        }

        private void WebClient_DownloadProgressChanged(object sender, DownloadProgressChangedEventArgs e)
        {
            switch (status)
            {
                case UpdateStatus.Downloading:
                    Updated?.Invoke(UpdateStatus.Downloading, e.ProgressPercentage);
                    break;
            }
        }

        private void WebClient_DownloadStringCompleted(object sender, DownloadStringCompletedEventArgs e)
        {
            // something went wrong with the connection
            if (e.Error != null)
            {
                Updated?.Invoke(UpdateStatus.Failed, e.Error);
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

        private void ParseMETA(string Result)
        {
            double asset_size = 0.0d;
            
            try
            {
                string assets = CommonUtils.Between(Result, "Assets</h3>", "</details>");

                asset_size = double.Parse(CommonUtils.Between(Result, "asset-size-label\">", " MB</span>"), CultureInfo.InvariantCulture);
            }
            catch (Exception) { }

            if (asset_size == 0.0d || updateFile.filesize == 0.0d)
            {
                status = UpdateStatus.Failed;
                Updated?.Invoke(status, updateFile);
                return;
            }

            status = UpdateStatus.Ready;
            Updated?.Invoke(status, updateFile);

            // download release
            webClient.DownloadFileAsync(updateFile.uri, updateFile.filename);
            status = UpdateStatus.Downloading;
            Updated?.Invoke(status, 0);
        }

        private void ParseATOM(string Result)
        {
            // pull build from github
            XmlDocument doc = new XmlDocument();
            doc.LoadXml(Result);

            XmlNodeList entries = doc.GetElementsByTagName("entry");
            var entry = entries[0];

            Version latestBuild = new Version(0,0,0,0);
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

#if !DEBUG
            if (latestBuild <= build)
            {
                Updated?.Invoke(UpdateStatus.Updated, null);
                return;
            }

            if (latestHref == null)
            {
                Updated?.Invoke(UpdateStatus.Failed, null);
                return;
            }
#endif

            status = UpdateStatus.CheckingMETA;
            webClient.DownloadStringAsync(latestHref);
        }

        public void Start()
        {
            DateTime dateTime = Properties.Settings.Default.UpdateLastChecked;
            lastchecked = dateTime;
            Updated?.Invoke(UpdateStatus.Initialized, null);
        }

        public DateTime GetTime()
        {
            return lastchecked;
        }

        public void UpdateTime()
        {
            lastchecked = DateTime.Now;
            Properties.Settings.Default.UpdateLastChecked = lastchecked;
            Properties.Settings.Default.Save();
        }

        public void StartProcess()
        {
            // Update UI
            status = UpdateStatus.CheckingATOM;
            Updated?.Invoke(status, null);

            // download github
            string restUrl = $"https://github.com/Valkirie/ControllerService/releases.atom?{random.Next().ToString()}";
            webClient.DownloadStringAsync(new Uri(restUrl));
        }

        public void InstallUpdate()
        {
            if (status != UpdateStatus.Downloaded)
                return;

            if (!File.Exists(updateFile.filename))
                return;

            Process.Start(updateFile.filename);
            Process.GetCurrentProcess().Kill();
        }
    }
}
