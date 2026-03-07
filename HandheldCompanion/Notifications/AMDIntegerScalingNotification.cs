using HandheldCompanion.Misc;
using HandheldCompanion.Utils;
using HandheldCompanion.Views;
using iNKORE.UI.WPF.Modern.Controls;
using System.Threading.Tasks;

namespace HandheldCompanion.Notifications
{
    public class AMDIntegerScalingNotification : Notification
    {
        private string targetRegistryPath;

        public AMDIntegerScalingNotification() : base(
            Properties.Resources.Hint_AMD_IntegerScalingCheck,
            Properties.Resources.Hint_AMD_IntegerScalingCheckDesc,
            Properties.Resources.Hint_AMD_IntegerScalingCheckAction,
            InfoBarSeverity.Error)
        { }

        public void SetTargetRegistryPath(string registryPath)
        {
            targetRegistryPath = registryPath;
        }

        public override async void Execute()
        {
            if (!string.IsNullOrEmpty(targetRegistryPath))
            {
                // Force-enable Embedded Integer Scaling support for the matched adapter
                RegistryUtils.SetValue(targetRegistryPath, "DalEmbeddedIntegerScalingSupport", 1);
            }

            Task<ContentDialogResult> dialogTask = new Dialog(MainWindow.GetCurrent())
            {
                Title = Properties.Resources.Dialog_ForceRestartTitle,
                Content = Properties.Resources.Dialog_ForceRestartDesc,
                DefaultButton = ContentDialogButton.Close,
                CloseButtonText = Properties.Resources.Dialog_No,
                PrimaryButtonText = Properties.Resources.Dialog_Yes
            }.ShowAsync();

            await dialogTask;

            if (dialogTask.Result == ContentDialogResult.Primary)
                DeviceUtils.RestartComputer();

            base.Execute();
        }
    }
}
