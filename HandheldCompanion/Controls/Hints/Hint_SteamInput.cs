using HandheldCompanion.Managers;
using System;
using System.Threading.Tasks;
using System.Windows;

namespace HandheldCompanion.Controls.Hints
{
    public class Hint_SteamInput : IHint
    {
        public Hint_SteamInput() : base()
        {
            PlatformManager.Steam.SettingValueChanged += Steam_SettingValueChanged;

            // default state
            this.HintActionButton.Visibility = Visibility.Visible;

            this.HintTitle.Text = Properties.Resources.Hint_SteamInput;
            this.HintDescription.Text = Properties.Resources.Hint_SteamInputDesc;
            this.HintReadMe.Text = Properties.Resources.Hint_SteamInputReadme;

            this.HintActionButton.Content = Properties.Resources.Hint_SteamInputAction;
        }

        private void Steam_SettingValueChanged(string name, object value)
        {
            // UI thread
            Application.Current.Dispatcher.Invoke(() =>
            {
                if (name.Equals("UseSteamControllerConfig"))
                {
                    int SteamInput = int.Parse(value.ToString());
                    switch (SteamInput)
                    {
                        case 0:
                            this.Visibility = Visibility.Collapsed;
                            break;

                        case 1:
                        case 2:
                            this.Visibility = Visibility.Visible;
                            break;
                    }
                }
            });
        }

        protected override void HintActionButton_Click(object sender, RoutedEventArgs e)
        {
            Task.Run(async () =>
            {
                // halt steam and wait
                PlatformManager.Steam.StopProcess();
                while (PlatformManager.Steam.IsRunning)
                    await Task.Delay(1000);

                // overwrite desktop layout
                PlatformManager.Steam.SetUseSteamControllerConfigValue(0);

                // restart steam
                PlatformManager.Steam.StartProcess();
            });
        }

        public override void Stop()
        {
            base.Stop();
        }
    }
}
