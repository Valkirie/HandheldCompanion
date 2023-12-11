using HandheldCompanion.Managers;
using HandheldCompanion.Platforms;
using System.Windows;

namespace HandheldCompanion.Controls.Hints
{
    public class Hint_SteamXboxDrivers : IHint
    {
        public Hint_SteamXboxDrivers() : base()
        {
            PlatformManager.Steam.Updated += Steam_Updated;
            PlatformManager.Initialized += PlatformManager_Initialized;

            // default state
            this.HintActionButton.Visibility = Visibility.Collapsed;

            this.HintTitle.Text = Properties.Resources.Hint_SteamXboxDrivers;
            this.HintDescription.Text = Properties.Resources.Hint_SteamXboxDriversDesc;
            this.HintReadMe.Text = Properties.Resources.Hint_SteamXboxDriversReadme;
        }

        private void Steam_Updated(PlatformStatus status)
        {
            CheckDrivers();
        }

        private void PlatformManager_Initialized()
        {
            CheckDrivers();
        }

        private void CheckDrivers()
        {
            bool HasXboxDriversInstalled = PlatformManager.Steam.HasXboxDriversInstalled();

            // UI thread (async)
            Application.Current.Dispatcher.BeginInvoke(() =>
            {
                this.Visibility = HasXboxDriversInstalled ? Visibility.Visible : Visibility.Collapsed;
            });
        }

        public override void Stop()
        {
            base.Stop();
        }
    }
}
