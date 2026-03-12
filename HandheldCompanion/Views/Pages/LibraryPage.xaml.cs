using HandheldCompanion.Converters;
using HandheldCompanion.ViewModels;
using iNKORE.UI.WPF.Modern.Controls;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace HandheldCompanion.Views.Pages;

public partial class LibraryPage : System.Windows.Controls.Page
{
    private AverageColorConverter averageColorConverter = new AverageColorConverter();

    public LibraryPage()
    {
        Tag = "about";
        DataContext = new LibraryPageViewModel(this);
        InitializeComponent();
    }

    public LibraryPage(string Tag) : this()
    {
        this.Tag = Tag;
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

    public void Page_Closed()
    { }

    /// <summary>
    /// Handles mouse wheel scrolling for the horizontal favorites ScrollViewer.
    /// - Horizontal scroll: Shift+MouseWheel
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
}