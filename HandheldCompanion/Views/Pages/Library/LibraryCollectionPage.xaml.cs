using System;
using HandheldCompanion.ViewModels;
using System.Windows.Controls;

namespace HandheldCompanion.Views.Pages.Library;

public partial class LibraryCollectionPage : Page, ILibraryRoutedPage
{
    public string NavigationKey { get; }

    public LibraryCollectionPage(LibraryPageViewModel viewModel, global::System.Guid collectionId)
    {
        DataContext = viewModel;
        NavigationKey = LibraryNavigationKeys.Collection(collectionId);
        InitializeComponent();
    }
}
