using HandheldCompanion.Managers;
using HandheldCompanion.Platforms;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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

            // default state
            this.HintActionButton.Visibility = Visibility.Visible;

            this.HintTitle.Text = Properties.Resources.Hint_SteamNeptuneDesktopWarning;
            this.HintDescription.Text = Properties.Resources.Hint_SteamNeptuneDesktopDesc;
            this.HintReadMe.Text = Properties.Resources.Hint_SteamNeptuneDesktopAction;

            this.HintActionButton.Content = "Restart Steam";
        }

        private void Steam_Updated(PlatformStatus status)
        {
            bool DesktopProfileApplied = PlatformManager.Steam.HasDesktopProfileApplied();

            // UI thread (async)
            Application.Current.Dispatcher.BeginInvoke(() =>
            {
                switch (status)
                {
                    case PlatformStatus.Stopping:
                    case PlatformStatus.Stopped:
                        this.Visibility = Visibility.Collapsed;
                        break;
                    default:
                    case PlatformStatus.Started:
                        this.Visibility = DesktopProfileApplied ? Visibility.Visible : Visibility.Collapsed;
                        break;
                }
            });
        }

        private void PlatformManager_Initialized()
        {
            Steam_Updated(PlatformManager.Steam.Status);
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
    }
}
