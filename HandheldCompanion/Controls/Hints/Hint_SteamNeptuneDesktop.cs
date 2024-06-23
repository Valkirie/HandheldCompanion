﻿using HandheldCompanion.Managers;
using HandheldCompanion.Platforms;
using System;
using System.Threading.Tasks;
using System.Windows;

namespace HandheldCompanion.Controls.Hints
{
    public class Hint_SteamNeptuneDesktop : IHint
    {
        public Hint_SteamNeptuneDesktop() : base()
        {
            PlatformManager.Steam.Updated += Steam_Updated;
            PlatformManager.Initialized += PlatformManager_Initialized;
            SettingsManager.SettingValueChanged += SettingsManager_SettingValueChanged;

            // default state
            this.HintActionButton.Visibility = Visibility.Visible;

            this.HintTitle.Text = Properties.Resources.Hint_SteamNeptuneDesktop;
            this.HintDescription.Text = Properties.Resources.Hint_SteamNeptuneDesktopDesc;
            this.HintReadMe.Text = Properties.Resources.Hint_SteamNeptuneReadme;

            this.HintActionButton.Content = Properties.Resources.Hint_SteamNeptuneAction;
        }

        private void Steam_Updated(PlatformStatus status)
        {            
            bool OverrideSteamProfileOnStart = SettingsManager.GetBoolean("OverrideSteamProfileOnStart");
            bool DesktopProfileApplied = PlatformManager.Steam.HasDesktopProfileApplied();

            // UI thread (async)
            Application.Current.Dispatcher.Invoke(() =>
            {
                switch (status)
                {
                    default:
                    case PlatformStatus.Stopping:
                    case PlatformStatus.Stopped:
                        this.Visibility = Visibility.Collapsed;
                        break;
                    case PlatformStatus.Started:

                        if (OverrideSteamProfileOnStart)
                        {
                            this.HintTitle.Text = Properties.Resources.Hint_SteamNeptuneDesktop;
                            this.HintDescription.Text = Properties.Resources.Hint_SteamNeptuneDesktopDesc;
                            this.HintReadMe.Text = Properties.Resources.Hint_SteamNeptuneReadme;

                            this.Visibility = DesktopProfileApplied ? Visibility.Visible : Visibility.Collapsed;
                        }
                        else
                        {
                            this.HintTitle.Text = Properties.Resources.Hint_SteamNeptuneDesktopOverridden;
                            this.HintDescription.Text = Properties.Resources.Hint_SteamNeptuneDesktopOverriddenDesc;
                            this.HintReadMe.Text = Properties.Resources.Hint_SteamNeptuneReadmeOverridden;

                            this.Visibility = !DesktopProfileApplied ? Visibility.Visible : Visibility.Collapsed;
                        }

                        break;
                }
            });
        }

        private void PlatformManager_Initialized()
        {
            Steam_Updated(PlatformManager.Steam.Status);
        }

        private void SettingsManager_SettingValueChanged(string name, object value)
        {
            switch (name)
            {
                case "OverrideSteamProfileOnStart":
                    Steam_Updated(PlatformManager.Steam.Status);
                    break;
            }
        }

        protected override void HintActionButton_Click(object sender, RoutedEventArgs e)
        {
            Task.Run(async () =>
            {
                PlatformManager.Steam.StopProcess();

                while (PlatformManager.Steam.IsRunning)
                    await Task.Delay(1000);

                PlatformManager.Steam.StartProcess();
            });
        }

        public override void Stop()
        {
            base.Stop();
        }
    }
}
