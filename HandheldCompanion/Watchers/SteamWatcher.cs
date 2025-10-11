using HandheldCompanion.Managers;
using HandheldCompanion.Notifications;
using HandheldCompanion.Platforms;
using iNKORE.UI.WPF.Modern.Controls;

namespace HandheldCompanion.Watchers
{
    public class SteamWatcher : ISpaceWatcher
    {
        private SteamInputsNotification steamInputsNotification = new SteamInputsNotification();
        private Notification driversNotification = new Notification(
            Properties.Resources.Hint_SteamXboxDrivers,
            Properties.Resources.Hint_SteamXboxDriversDesc,
            string.Empty,
            InfoBarSeverity.Error);

        public SteamWatcher()
        { }

        public override void Start()
        {
            PlatformManager.Steam.Updated += Steam_Updated;
            PlatformManager.Steam.SettingValueChanged += Steam_SettingValueChanged;
            base.Start();
        }

        public override void Stop()
        {
            PlatformManager.Steam.Updated -= Steam_Updated;
            PlatformManager.Steam.SettingValueChanged -= Steam_SettingValueChanged;

            base.Stop();
        }

        private void Steam_Updated(PlatformStatus status)
        {
            CheckDrivers();
        }

        private void Steam_SettingValueChanged(string name, object value)
        {
            if (name.Equals("UseSteamControllerConfig"))
            {
                int SteamInput = int.Parse(value.ToString());
                switch (SteamInput)
                {
                    case 0:
                        ManagerFactory.notificationManager.Discard(steamInputsNotification);
                        break;
                    case 1:
                    case 2:
                        ManagerFactory.notificationManager.Add(steamInputsNotification);
                        break;
                }
            }
        }

        private void CheckDrivers()
        {
            bool HasXboxDriversInstalled = PlatformManager.Steam.HasXboxDriversInstalled();
            switch (HasXboxDriversInstalled)
            {
                case true:
                    ManagerFactory.notificationManager.Add(driversNotification);
                    break;
                case false:
                    ManagerFactory.notificationManager.Discard(driversNotification);
                    break;
            }
        }
    }
}
