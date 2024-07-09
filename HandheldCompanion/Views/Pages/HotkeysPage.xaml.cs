using HandheldCompanion.Controls;
using HandheldCompanion.Devices;
using HandheldCompanion.Extensions;
using HandheldCompanion.Managers;
using HandheldCompanion.Utils;
using HandheldCompanion.ViewModels;
using iNKORE.UI.WPF.Modern.Controls;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using Page = System.Windows.Controls.Page;

namespace HandheldCompanion.Views.Pages;

/// <summary>
///     Interaction logic for HotkeysPage.xaml
/// </summary>
public partial class HotkeysPage : Page
{
    public HotkeysPage()
    {
        DataContext = new HotkeyPageViewModel();
        InitializeComponent();
    }

    public HotkeysPage(string Tag) : this()
    {
        this.Tag = Tag;
    }

    private void Page_Loaded(object sender, RoutedEventArgs e)
    {
    }

    public void Page_Closed() { }
}