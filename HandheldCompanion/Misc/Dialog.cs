using HandheldCompanion.Views;
using HandheldCompanion.Views.Windows;
using iNKORE.UI.WPF.Modern.Controls;
using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using System.Windows;

namespace HandheldCompanion.Misc
{
    public class Dialog
    {
        private static readonly ConcurrentDictionary<Window, bool> _openDialogs = new();

        private readonly ContentDialog _dialog;
        private readonly Window _owner;

        public string Title { get; set; }
        public object Content { get; set; }
        public ContentDialogButton DefaultButton { get; set; } = ContentDialogButton.Primary;
        public string PrimaryButtonText { get; set; } = string.Empty;
        public string SecondaryButtonText { get; set; } = string.Empty;
        public string CloseButtonText { get; set; } = string.Empty;
        public bool CanClose { get; set; } = true;

        public Dialog(Window owner)
        {
            _owner = owner ?? throw new ArgumentNullException(nameof(owner));

            switch (owner.Tag as string)
            {
                default:
                case "MainWindow":
                    _dialog = MainWindow.GetCurrent().ContentDialog;
                    break;
                case "QuickTools":
                    _dialog = OverlayQuickTools.GetCurrent().ContentDialog;
                    break;
            }

            _dialog.Closing += OnDialogClosing;
        }

        public async Task<ContentDialogResult> ShowAsync()
        {
            if (!_openDialogs.TryAdd(_owner, true))
                return ContentDialogResult.None;

            try
            {
                ApplyProperties();

                // attempt to show; if another dialog is open internally,
                // this will throw and we catch below
                return await _dialog.ShowAsync(_owner);
            }
            catch (InvalidOperationException)
            {
                return ContentDialogResult.None;
            }
            finally
            {
                _openDialogs.TryRemove(_owner, out _);
            }
        }

        public void Hide()
        {
            CanClose = true;
            _dialog.Hide();
            _openDialogs.TryRemove(_owner, out _);
        }

        public void UpdateTitle(string Title)
        {
            _dialog.Title = this.Title = Title;
        }

        public void UpdateContent(object Content)
        {
            _dialog.Content = this.Content = Content;
        }

        private void ApplyProperties()
        {
            _dialog.Title = Title;
            _dialog.Content = Content;
            _dialog.PrimaryButtonText = PrimaryButtonText;
            _dialog.SecondaryButtonText = SecondaryButtonText;
            _dialog.CloseButtonText = CloseButtonText;
            _dialog.DefaultButton = DefaultButton;
        }

        private void OnDialogClosing(ContentDialog sender, ContentDialogClosingEventArgs args)
        {
            if (!CanClose)
                args.Cancel = true;
            else
                sender.Closing -= OnDialogClosing;
        }
    }
}