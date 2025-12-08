using HandheldCompanion.Helpers;
using HandheldCompanion.Managers;
using HandheldCompanion.Views.Windows;
using System.Windows;
using System.Windows.Input;

namespace HandheldCompanion.ViewModels
{
    public class OverlayQuickToolsViewModel : BaseViewModel
    {
        public Visibility QuickKeyboardVisibility => ManagerFactory.settingsManager.GetBoolean("QuickKeyboardVisibility") ? Visibility.Visible : Visibility.Collapsed;
        public Visibility QuickTrackpadVisibility => ManagerFactory.settingsManager.GetBoolean("QuickTrackpadVisibility") ? Visibility.Visible : Visibility.Collapsed;
        public bool QuickToolsApplyNoise => ManagerFactory.settingsManager.GetBoolean("QuickToolsApplyNoise");

        public ICommand PowerDropCommand { get; private set; }

        private OverlayQuickTools overlayQuickTools;
        public OverlayQuickToolsViewModel(OverlayQuickTools overlayQuickTools)
        {
            this.overlayQuickTools = overlayQuickTools;

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

            PowerDropCommand = new DelegateCommand<string>(async (action) =>
            {
                switch (action)
                {
                    case "Sleep": PowerActions.Sleep(force: false); break;
                    case "Shutdown": PowerActions.Shutdown(force: false, powerOff: true); break;
                    case "Restart": PowerActions.Restart(force: false); break;
                    case "Lock": PowerActions.Lock(); break;
                }
            });
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

            // force (re)apply style
            overlayQuickTools?.UpdateStyle();
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
