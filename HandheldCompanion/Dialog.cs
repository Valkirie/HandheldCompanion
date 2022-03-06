using ModernWpf.Controls;
using System.Threading.Tasks;

namespace HandheldCompanion
{
    internal class Dialog
    {
        public static async Task<ContentDialogResult> ShowAsync(string Title, string Content, ContentDialogButton DefaultButton = ContentDialogButton.Primary, string CloseButtonText = null, string PrimaryButtonText = null, string SecondaryButtonText = null)
        {
            ContentDialog dialog = new ContentDialog
            {
                Title = Title,
                Content = Content,
                CloseButtonText = CloseButtonText,
                PrimaryButtonText = PrimaryButtonText,
                SecondaryButtonText = SecondaryButtonText,
                DefaultButton = DefaultButton
            };

            ContentDialogResult result = await dialog.ShowAsync();
            return result;
        }
    }
}
