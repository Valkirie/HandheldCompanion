using ModernWpf.Controls;

namespace ControllerHelperWPF
{
    internal class Common
    {
        public static void ShowDialog(string title, string content)
        {
            ContentDialog noWifiDialog = new ContentDialog
            {
                Title = title,
                Content = content,
                CloseButtonText = "Ok"
            };

            noWifiDialog.ShowAsync();
        }
    }
}
