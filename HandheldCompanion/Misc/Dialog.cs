using Inkore.UI.WPF.Modern.Controls;
using System.Threading.Tasks;

namespace HandheldCompanion.Misc;

internal class Dialog
{
    public static async Task<ContentDialogResult> ShowAsync(string Title, string Content,
        ContentDialogButton DefaultButton = ContentDialogButton.Primary, string CloseButtonText = null,
        string PrimaryButtonText = null, string SecondaryButtonText = null)
    {
        try
        {
            var dialog = new ContentDialog
            {
                Title = Title,
                Content = Content,
                CloseButtonText = CloseButtonText,
                PrimaryButtonText = PrimaryButtonText,
                SecondaryButtonText = SecondaryButtonText,
                DefaultButton = DefaultButton
            };

            var result = await dialog.ShowAsync();
            return result;
        }
        catch
        {
        }

        return ContentDialogResult.None;
    }
}