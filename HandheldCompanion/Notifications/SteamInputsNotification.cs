using HandheldCompanion.Managers;
using iNKORE.UI.WPF.Modern.Controls;
using System;
using System.Threading.Tasks;

namespace HandheldCompanion.Notifications
{
    public class SteamInputsNotification : Notification
    {
        public SteamInputsNotification() : base(
            Properties.Resources.Hint_SteamInput,
            Properties.Resources.Hint_SteamInputDesc,
            Properties.Resources.Hint_SteamInputAction,
            InfoBarSeverity.Error)
        { }

        public override void Execute()
        {
            if (!ManagerFactory.platformManager.IsReady)
                return;

            Task.Run(async () =>
            {
                // halt steam and wait
                PlatformManager.Steam.StopProcess();
                while (PlatformManager.Steam.IsRunning)
                    await Task.Delay(1000).ConfigureAwait(false); // Avoid blocking the synchronization context;

                // overwrite desktop layout
                PlatformManager.Steam.SetUseSteamControllerConfigValue(0);

                // restart steam
                PlatformManager.Steam.StartProcess();
            });

            base.Execute();
        }
    }
}
