using ControllerCommon;
using ControllerCommon.Managers;
using ControllerCommon.Platforms;
using HandheldCompanion.Platforms;
using HandheldCompanion.Views;
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
            ProfileManager.Applied += ProfileManager_Applied;

            IsInitialized = true;

            LogManager.LogInformation("{0} has started", "PlatformManager");
        }

        private static void ProfileManager_Applied(Profile profile)
        {
            // AutoTDP
            if (profile.AutoTDPEnabled)
                CurrentNeeds |= PlatformNeeds.AutoTDP;
            else
                CurrentNeeds &= ~PlatformNeeds.AutoTDP;

            // Framerate limiter
            if (profile.FramerateEnabled)
                CurrentNeeds |= PlatformNeeds.FramerateLimiter;
            else
                CurrentNeeds &= ~PlatformNeeds.FramerateLimiter;

            MonitorPlatforms();
        }

        [Flags]
        private enum PlatformNeeds
        {
            None = 0,
            AutoTDP = 1,
            FramerateLimiter = 2,
            OnScreenDisplay = 4,
            OnScreenDisplayComplex = 8,
        }

        private static PlatformNeeds CurrentNeeds = PlatformNeeds.None;
        private static PlatformNeeds PreviousNeeds = PlatformNeeds.None;

        private static void SettingsManager_SettingValueChanged(string name, object value)
        {
            // UI thread (async)
            Application.Current.Dispatcher.BeginInvoke(() =>
            {
                switch (name)
                {
                    case "OnScreenDisplayLevel":
                        {
                            short level = Convert.ToInt16(value);

                            switch(level)
                            {
                                case 0:
                                    CurrentNeeds &= ~PlatformNeeds.OnScreenDisplay;
                                    CurrentNeeds &= ~PlatformNeeds.OnScreenDisplayComplex;
                                    break;
                                default:
                                case 1:
                                    CurrentNeeds |= PlatformNeeds.OnScreenDisplay;
                                    CurrentNeeds &= ~PlatformNeeds.OnScreenDisplayComplex;
                                    break;
                                case 2:
                                case 3:
                                    CurrentNeeds |= PlatformNeeds.OnScreenDisplayComplex;
                                    break;
                            }

                            MonitorPlatforms();
                        }
                        break;

                    /*
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
                    */
                }
            });
        }

        private static void MonitorPlatforms()
        {
            /* 
             * Dependencies: 
             * HWInfo: OSD 
             * RTSS: AutoTDP, framerate limiter, OSD 
             */

            // Check if the current needs are the same as the previous needs
            if (CurrentNeeds == PreviousNeeds) return;

            // Start or stop HWiNFO and RTSS based on the current and previous needs
            if (CurrentNeeds.HasFlag(PlatformNeeds.OnScreenDisplay))
            {
                // If OSD is needed, start RTSS and start HWiNFO only if OnScreenDisplayComplex is true
                if (!PreviousNeeds.HasFlag(PlatformNeeds.OnScreenDisplay))
                {
                    // Only start RTSS if it was not running before and if it is installed
                    if (RTSS.IsInstalled)
                    {
                        RTSS.Start();
                    }
                }
                if (CurrentNeeds.HasFlag(PlatformNeeds.OnScreenDisplayComplex))
                {
                    // This condition checks if OnScreenDisplayComplex is true
                    // OnScreenDisplayComplex is a new flag that indicates if the OSD needs more information from HWiNFO
                    if (!PreviousNeeds.HasFlag(PlatformNeeds.OnScreenDisplay) || !PreviousNeeds.HasFlag(PlatformNeeds.OnScreenDisplayComplex))
                    {
                        // Only start HWiNFO if it was not running before or if OnScreenDisplayComplex was false and if it is installed
                        if (HWiNFO.IsInstalled)
                        {
                            HWiNFO.Start();
                        }
                    }
                }
                else
                {
                    // If OnScreenDisplayComplex is false, stop HWiNFO if it was running before
                    if (PreviousNeeds.HasFlag(PlatformNeeds.OnScreenDisplay) && PreviousNeeds.HasFlag(PlatformNeeds.OnScreenDisplayComplex))
                    {
                        // Only stop HWiNFO if it is installed
                        if (HWiNFO.IsInstalled)
                        {
                            HWiNFO.Stop(true);
                        }
                    }
                }
            }
            else if (CurrentNeeds.HasFlag(PlatformNeeds.AutoTDP) || CurrentNeeds.HasFlag(PlatformNeeds.FramerateLimiter))
            {
                // If AutoTDP or framerate limiter is needed, start only RTSS and stop HWiNFO
                if (!PreviousNeeds.HasFlag(PlatformNeeds.AutoTDP) && !PreviousNeeds.HasFlag(PlatformNeeds.FramerateLimiter))
                {
                    // Only start RTSS if it was not running before and if it is installed
                    if (RTSS.IsInstalled)
                    {
                        RTSS.Start();
                    }
                }
                if (PreviousNeeds.HasFlag(PlatformNeeds.OnScreenDisplay))
                {
                    // Only stop HWiNFO if it was running before
                    // Only stop HWiNFO if it is installed
                    if (HWiNFO.IsInstalled)
                    {
                        HWiNFO.Stop(true);
                    }
                }
            }
            else
            {
                // If none of the needs are present, stop both HWiNFO and RTSS
                if (PreviousNeeds.HasFlag(PlatformNeeds.OnScreenDisplay) || PreviousNeeds.HasFlag(PlatformNeeds.AutoTDP) || PreviousNeeds.HasFlag(PlatformNeeds.FramerateLimiter))
                {
                    // Only stop HWiNFO and RTSS if they were running before and if they are installed
                    if (HWiNFO.IsInstalled)
                    {
                        HWiNFO.Stop(true);
                    }
                    if (RTSS.IsInstalled)
                    {
                        RTSS.Stop();
                    }
                }
            }

            // Store the current needs in the previous needs variable
            PreviousNeeds = CurrentNeeds;
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
