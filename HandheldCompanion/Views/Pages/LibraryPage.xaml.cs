using HandheldCompanion.Converters;
using HandheldCompanion.ViewModels;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;

namespace HandheldCompanion.Views.Pages;

public partial class LibraryPage : Page
{
    private const int WM_MOUSEHWHEEL = 0x020E;
    private AverageColorConverter averageColorConverter = new AverageColorConverter();
    private HwndSource hwndSource;

    public LibraryPage()
    {
        Tag = "about";
        DataContext = new LibraryPageViewModel(this);
        InitializeComponent();

        Loaded += LibraryPage_Loaded;
        Unloaded += LibraryPage_Unloaded;
    }

    public LibraryPage(string Tag) : this()
    {
        this.Tag = Tag;
    }

    private void LibraryPage_Loaded(object sender, RoutedEventArgs e)
    {
        // Hook into Windows message pump for horizontal mouse wheel
        Window window = Window.GetWindow(this);
        if (window != null)
        {
            hwndSource = PresentationSource.FromVisual(window) as HwndSource;
            hwndSource?.AddHook(WndProc);
        }
    }

    private void LibraryPage_Unloaded(object sender, RoutedEventArgs e)
    {
        // Unhook from Windows message pump
        hwndSource?.RemoveHook(WndProc);
        hwndSource = null;
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_MOUSEHWHEEL)
        {
            // Check if mouse is over the favorites ScrollViewer
            if (favoritesScrollViewer != null && favoritesScrollViewer.IsMouseOver)
            {
                // Extract delta from wParam (high word)
                int delta = (short)((int)wParam >> 16);

                // Horizontal wheel: positive delta = scroll right, negative = scroll left
                // Note: Some mice may have inverted delta, but this is the Windows standard
                if (delta > 0)
                    favoritesScrollViewer.LineRight();
                else if (delta < 0)
                    favoritesScrollViewer.LineLeft();

                handled = true;
            }
        }

        return IntPtr.Zero;
    }

    private void ImageContainer_GotFocus(object sender, RoutedEventArgs e)
    {
        UpdateArtwork(sender);
    }

    private void ImageContainer_MouseEnter(object sender, MouseEventArgs e)
    {
        UpdateArtwork(sender);
    }

    private void UpdateArtwork(object sender)
    {
        if (sender is Button button && button.DataContext is ProfileViewModel profile)
        {
            LibraryPageViewModel? libraryVM = DataContext as LibraryPageViewModel;
            if (libraryVM != null)
            {
                libraryVM.HighlightColor = (Color)averageColorConverter.Convert(profile.Cover, null, null, null);
                libraryVM.Artwork = profile.Artwork;
            }
        }
    }

    private void Page_Loaded(object sender, RoutedEventArgs e)
    { }

    /// <summary>
    /// Handles mouse wheel scrolling for the horizontal favorites ScrollViewer.
    /// - Horizontal scroll: Shift+MouseWheel OR native horizontal wheel (via WndProc)
    /// - Vertical scroll: Bubbles to parent ScrollViewer
    /// </summary>
    private void FavoritesScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (sender is not ScrollViewer scrollViewer)
            return;

        // Allow horizontal scrolling with Shift key
        if (Keyboard.Modifiers == ModifierKeys.Shift)
        {
            if (e.Delta > 0)
                scrollViewer.LineLeft();
            else
                scrollViewer.LineRight();

            e.Handled = true;
            return;
        }

        // For vertical scrolling (no Shift), bubble event to parent ScrollViewer
        // This allows the main page to scroll vertically even when mouse is over favorites
        e.Handled = true;

        var parentScrollViewer = FindParentScrollViewer(scrollViewer);
        if (parentScrollViewer != null)
        {
            var newEvent = new MouseWheelEventArgs(e.MouseDevice, e.Timestamp, e.Delta)
            {
                RoutedEvent = UIElement.MouseWheelEvent,
                Source = scrollViewer
            };
            parentScrollViewer.RaiseEvent(newEvent);
        }
    }

    private ScrollViewer FindParentScrollViewer(DependencyObject child)
    {
        DependencyObject parent = VisualTreeHelper.GetParent(child);
        while (parent != null)
        {
            if (parent is ScrollViewer sv)
                return sv;
            parent = VisualTreeHelper.GetParent(parent);
        }
        return null;
    }

    public void Page_Closed()
    { }
}