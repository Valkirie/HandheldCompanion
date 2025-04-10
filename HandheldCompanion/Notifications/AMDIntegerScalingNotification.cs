using HandheldCompanion.Misc;
using HandheldCompanion.Utils;
using HandheldCompanion.Views;
using iNKORE.UI.WPF.Modern.Controls;
using System.Threading.Tasks;

namespace HandheldCompanion.Notifications
{
    public class AMDIntegerScalingNotification : Notification
    {
        public AMDIntegerScalingNotification() : base(
            Properties.Resources.Hint_AMD_IntegerScalingCheck,
            Properties.Resources.Hint_AMD_IntegerScalingCheckDesc,
            Properties.Resources.Hint_AMD_IntegerScalingCheckAction,
            InfoBarSeverity.Error)
        { }

        public override async void Execute()
        {
            RegistryUtils.SetValue(@"SYSTEM\ControlSet001\Control\Class\{4d36e968-e325-11ce-bfc1-08002be10318}\0000", "DalEmbeddedIntegerScalingSupport", 1);

            Task<ContentDialogResult> dialogTask = new Dialog(MainWindow.GetCurrent())
            {
                Title = Properties.Resources.Dialog_ForceRestartTitle,
                Content = Properties.Resources.Dialog_ForceRestartDesc,
                DefaultButton = ContentDialogButton.Close,
                CloseButtonText = Properties.Resources.Dialog_No,
                PrimaryButtonText = Properties.Resources.Dialog_Yes
            }.ShowAsync();

            await dialogTask; // sync call

            switch (dialogTask.Result)
            {
                case ContentDialogResult.Primary:
                    DeviceUtils.RestartComputer();
                    break;
                case ContentDialogResult.Secondary:
                    break;
            }

            base.Execute();
        }
    }
}
