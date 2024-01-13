using HandheldCompanion.Utils;
using HandheldCompanion.Views;
using HandheldCompanion.Views.Windows;
using iNKORE.UI.WPF.Modern.Controls;
using System.Threading.Tasks;
using System.Windows;

namespace HandheldCompanion.Misc;

internal class Dialog
{
    public static async Task<ContentDialogResult> ShowAsync(string Title, string Content, ContentDialogButton DefaultButton = ContentDialogButton.Primary,
        string CloseButtonText = null, string PrimaryButtonText = null, string SecondaryButtonText = null, Window owner = null)
    {
        try
        {
            // I hate my life... Improve me!
            ContentDialog dialog = null;
            switch (owner.Tag)
            {
                default:
                case "MainWindow":
                    dialog = MainWindow.GetCurrent().ContentDialog;
                    break;
                case "QuickTools":
                    dialog = OverlayQuickTools.GetCurrent().ContentDialog;
                    break;
            }

            dialog.Title = Title;
            dialog.Content = Content;
            dialog.CloseButtonText = CloseButtonText;
            dialog.PrimaryButtonText = PrimaryButtonText;
            dialog.SecondaryButtonText = SecondaryButtonText;
            dialog.DefaultButton = DefaultButton;

            ContentDialogResult result = await dialog.ShowAsync(owner);
            return result;
        }
        catch { }

        return ContentDialogResult.None;
    }
}