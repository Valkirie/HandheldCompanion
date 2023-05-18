using ControllerCommon.Managers;
using ControllerCommon.Platforms;
using HandheldCompanion.Platforms;
using System;
using System.Diagnostics;
using System.Threading;
using System.Windows;

namespace HandheldCompanion.Managers
{
    public static class PlatformManager
    {
        // gaming platforms
        private static SteamPlatform Steam = new();
        private static GOGGalaxy GOGGalaxy = new();
        private static UbisoftConnect UbisoftConnect = new();

        // misc platforms
        public static RTSS RTSS = new();
        public static HWiNFO HWiNFO = new();

        private static bool IsInitialized;

        public static void Start()
        {
            if (Steam.IsInstalled)
            {
                // overwrite controller files
                foreach (var config in SteamPlatform.ControllerFiles)
                    Steam.OverwriteFile(config.Key, config.Value, true);
            }

            if (GOGGalaxy.IsInstalled)
            {
                // do something
            }

            if (UbisoftConnect.IsInstalled)
            {
            }

            if (RTSS.IsInstalled)
            {
                // do something
            }

            if (HWiNFO.IsInstalled)
            {
                // do something
            }

            SettingsManager.SettingValueChanged += SettingsManager_SettingValueChanged;

            IsInitialized = true;

            LogManager.LogInformation("{0} has started", "PlatformManager");
        }

        private static void SettingsManager_SettingValueChanged(string name, object value)
        {
            // UI thread (async)
            Application.Current.Dispatcher.BeginInvoke(() =>
            {
                switch (name)
                {
                    case "PlatformRTSSEnabled":
                        {
                            if (!RTSS.IsInstalled)
                                return;

                            bool toggle = Convert.ToBoolean(value);

                            new Thread(() =>
                            {
                                switch (toggle)
                                {
                                    case true:
                                        RTSS.Start();
                                        break;
                                    case false:
                                        RTSS.Stop();
                                        break;
                                }
                            }).Start();
                        }
                        break;

                    case "PlatformHWiNFOEnabled":
                        {
                            if (!HWiNFO.IsInstalled)
                                return;

                            bool toggle = Convert.ToBoolean(value);

                            new Thread(() =>
                            {
                                switch (toggle)
                                {
                                    case true:
                                        HWiNFO.Start();
                                        break;
                                    case false:
                                        HWiNFO.Stop();
                                        break;
                                }
                            }).Start();
                        }
                        break;
                }
            });
        }

        public static void Stop()
        {
            if (Steam.IsInstalled)
            {
                // restore controller files
                foreach (var config in SteamPlatform.ControllerFiles)
                    Steam.ResetFile(config.Key);

                Steam.Dispose();
            }

            if (GOGGalaxy.IsInstalled)
            {
                GOGGalaxy.Dispose();
            }

            if (UbisoftConnect.IsInstalled)
            {
                UbisoftConnect.Dispose();
            }

            if (RTSS.IsInstalled)
            {
                RTSS.Stop();
                RTSS.Dispose();
            }

            if (HWiNFO.IsInstalled)
            {
                HWiNFO.Stop();
                HWiNFO.Dispose();
            }

            IsInitialized = false;

            LogManager.LogInformation("{0} has stopped", "PlatformManager");
        }

        public static PlatformType GetPlatform(Process proc)
        {
            if (!IsInitialized)
                return PlatformType.Windows;

            // is this process part of a specific platform
            if (Steam.IsRelated(proc))
                return Steam.PlatformType;
            else if (GOGGalaxy.IsRelated(proc))
                return GOGGalaxy.PlatformType;
            else if (UbisoftConnect.IsRelated(proc))
                return UbisoftConnect.PlatformType;
            else
                return PlatformType.Windows;
        }
    }
}
