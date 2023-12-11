using HandheldCompanion.Managers;
using HandheldCompanion.Platforms;
using System;
using System.Windows;

namespace HandheldCompanion.Controls.Hints
{
    public class Hint_HWiNFO12hLimitPassed : IHint
    {
        public Hint_HWiNFO12hLimitPassed() : base()
        {
            PlatformManager.HWiNFO.Updated += HWiNFO_Updated;
            PlatformManager.HWiNFO.SettingValueChanged += HWiNFO_SettingValueChanged;

            PlatformManager.Initialized += PlatformManager_Initialized;

            // default state
            this.HintActionButton.Visibility = Visibility.Collapsed;

            this.HintTitle.Text = Properties.Resources.Hint_HWiNFO12hLimitPassed;
            this.HintDescription.Text = Properties.Resources.Hint_HWiNFO12hLimitPassedDesc;
            this.HintReadMe.Text = Properties.Resources.Hint_HWiNFO12hLimitPassedReadme;
        }

        private void HWiNFO_Updated(PlatformStatus status)
        {
            CheckSettings();
        }

        private void HWiNFO_SettingValueChanged(string name, object value)
        {
            // UI thread (async)
            Application.Current.Dispatcher.BeginInvoke(() =>
            {
                switch(name)
                {
                    case "SensorsSM":
                        this.Visibility = Convert.ToBoolean(value) ? Visibility.Collapsed : Visibility.Visible;
                        break;
                }
            });
        }

        private void PlatformManager_Initialized()
        {
            CheckSettings();
        }

        private void CheckSettings()
        {
            bool SensorsSM = PlatformManager.HWiNFO.GetProperty("SensorsSM");
            HWiNFO_SettingValueChanged("SensorsSM", SensorsSM);
        }

        public override void Stop()
        {
            base.Stop();
        }
    }
}
