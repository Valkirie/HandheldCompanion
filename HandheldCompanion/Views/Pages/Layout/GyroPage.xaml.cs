using System.Windows;

namespace HandheldCompanion.Views.Pages;

/// <summary>
///     Interaction logic for GyroPage.xaml
/// </summary>
public partial class GyroPage : ILayoutPage
{
    public GyroPage()
    {
        InitializeComponent();
    }

    public GyroPage(string Tag) : this()
    {
        this.Tag = Tag;
    }

    private void Page_Loaded(object sender, RoutedEventArgs e)
    {
    }

    public void Page_Closed()
    {
    }
}