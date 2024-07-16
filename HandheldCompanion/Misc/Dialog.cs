using HandheldCompanion.Views;
using HandheldCompanion.Views.Windows;
using iNKORE.UI.WPF.Modern.Controls;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
namespace HandheldCompanion.Misc;

public class Dialog
{
    private static Dictionary<Window, Dialog> activeDialogs = [];

    private Window owner;
    private ContentDialog dialog;

    public string Title;
    public string Content;
    public ContentDialogButton DefaultButton = ContentDialogButton.Primary;
    public string PrimaryButtonText = string.Empty;
    public string SecondaryButtonText = string.Empty;
    public string CloseButtonText = string.Empty;

    public bool CanClose = true;

    public Dialog(Window owner)
    {
        this.owner = owner;

        // not my proudest code
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

        if (dialog is not null)
            dialog.Closing += Dialog_Closing;
    }

    public async Task<ContentDialogResult> ShowAsync()
    {
        if (activeDialogs.ContainsKey(owner))
        {
            return ContentDialogResult.None; // A dialog is already shown for this window
        }

        activeDialogs[owner] = this;

        try
        {
            dialog.Title = this.Title;
            dialog.Content = this.Content;
            dialog.CloseButtonText = this.CloseButtonText;
            dialog.PrimaryButtonText = this.PrimaryButtonText;
            dialog.SecondaryButtonText = this.SecondaryButtonText;
            dialog.DefaultButton = this.DefaultButton;

            ContentDialogResult result = await dialog.ShowAsync(owner);
            return result;
        }
        catch
        {
            return ContentDialogResult.None;
        }
        finally
        {
            activeDialogs.Remove(owner);
        }
    }

    public void Show()
    {
        if (activeDialogs.ContainsKey(owner))
        {
            return; // A dialog is already shown for this window
        }

        activeDialogs[owner] = this;

        try
        {
            dialog.Title = this.Title;
            dialog.Content = this.Content;
            dialog.CloseButtonText = this.CloseButtonText;
            dialog.PrimaryButtonText = this.PrimaryButtonText;
            dialog.SecondaryButtonText = this.SecondaryButtonText;
            dialog.DefaultButton = this.DefaultButton;

            dialog.ShowAsync(owner);
        }
        catch
        {
            activeDialogs.Remove(owner);
        }
    }

    public void Hide()
    {
        CanClose = true;
        dialog.Hide();
        activeDialogs.Remove(owner);
    }

    private void Dialog_Closing(ContentDialog sender, ContentDialogClosingEventArgs args)
    {
        args.Cancel = !CanClose;
    }

    public void UpdateTitle(string Title)
    {
        dialog.Title = this.Title = Title;
    }

    public void UpdateContent(string Content)
    {
        dialog.Content = this.Content = Content;
    }
}