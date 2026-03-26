using HandheldCompanion.Controls;
using HandheldCompanion.Converters;
using HandheldCompanion.ViewModels;
using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace HandheldCompanion.Views.Pages;

public partial class LibraryPage : Page
{
    private readonly AverageColorConverter averageColorConverter = new AverageColorConverter();
    private LibraryPageViewModel? libraryPageViewModel;

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

    private void LibraryPage_Loaded(object sender, RoutedEventArgs e)
    {
        AttachViewModel(DataContext as LibraryPageViewModel);
        UpdateItemsPanel();
    }

    private void LibraryPage_Unloaded(object sender, RoutedEventArgs e)
    {
        DetachViewModel();
    }

    private void AttachViewModel(LibraryPageViewModel? viewModel)
    {
        if (ReferenceEquals(libraryPageViewModel, viewModel))
            return;

        DetachViewModel();
        libraryPageViewModel = viewModel;

        if (libraryPageViewModel is not null)
            libraryPageViewModel.PropertyChanged += LibraryPageViewModel_PropertyChanged;
    }

    private void DetachViewModel()
    {
        if (libraryPageViewModel is not null)
            libraryPageViewModel.PropertyChanged -= LibraryPageViewModel_PropertyChanged;

        libraryPageViewModel = null;
    }

    private void LibraryPageViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(LibraryPageViewModel.ViewMode)
            or nameof(LibraryPageViewModel.IsGridView)
            or nameof(LibraryPageViewModel.IsListView))
        {
            Dispatcher.Invoke(UpdateItemsPanel);
        }
    }

    private void UpdateItemsPanel()
    {
        FrameworkElementFactory factory;

        if (libraryPageViewModel?.IsListView is true)
        {
            factory = new FrameworkElementFactory(typeof(StackPanel));
        }
        else
        {
            factory = new FrameworkElementFactory(typeof(JustifiedWrapPanel));
            factory.SetValue(JustifiedWrapPanel.HorizontalSpacingProperty, 6.0);
            factory.SetValue(JustifiedWrapPanel.VerticalSpacingProperty, 6.0);
            factory.SetValue(JustifiedWrapPanel.TargetRowHeightProperty, 300.0d);
            factory.SetValue(JustifiedWrapPanel.ItemAspectRatioProperty, 475.0 / 900.0);
        }

        profilesRepeater.ItemsPanel = new ItemsPanelTemplate(factory);
    }
}
