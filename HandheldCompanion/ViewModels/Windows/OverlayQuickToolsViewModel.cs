using HandheldCompanion.Managers;
using System.Windows;

namespace HandheldCompanion.ViewModels
{
    public class OverlayQuickToolsViewModel : BaseViewModel
    {
        public Visibility QuickKeyboardVisibility => ManagerFactory.settingsManager.GetBoolean("QuickKeyboardVisibility") ? Visibility.Visible : Visibility.Collapsed;
        public Visibility QuickTrackpadVisibility => ManagerFactory.settingsManager.GetBoolean("QuickTrackpadVisibility") ? Visibility.Visible : Visibility.Collapsed;
        public bool QuickToolsApplyNoise => ManagerFactory.settingsManager.GetBoolean("QuickToolsApplyNoise");

        public OverlayQuickToolsViewModel()
        {
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
        }

        private void SettingsManager_Initialized()
        {
            QuerySettings();
        }

        private void QuerySettings()
        {
            // manage events
            ManagerFactory.settingsManager.SettingValueChanged += SettingsManager_SettingValueChanged;

            // raise events
            /*
             * case "QuickKeyboardVisibility":
             * case "QuickTrackpadVisibility":
            */

            OnPropertyChanged("QuickKeyboardVisibility");
            OnPropertyChanged("QuickTrackpadVisibility");
            OnPropertyChanged("QuickToolsApplyNoise");
        }

        private void SettingsManager_SettingValueChanged(string name, object value, bool temporary)
        {
            switch (name)
            {
                case "QuickKeyboardVisibility":
                case "QuickTrackpadVisibility":
                case "QuickToolsApplyNoise":
                    OnPropertyChanged(name);
                    break;
            }
        }

        public override void Dispose()
        {
            // manage events
            ManagerFactory.settingsManager.Initialized -= SettingsManager_Initialized;
            ManagerFactory.settingsManager.SettingValueChanged -= SettingsManager_SettingValueChanged;

            base.Dispose();
        }
    }
}
