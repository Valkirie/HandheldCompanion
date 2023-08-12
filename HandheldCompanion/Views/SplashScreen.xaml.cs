using HandheldCompanion.Views.Classes;
using System.Windows;

namespace HandheldCompanion.Views
{
    /// <summary>
    /// Interaction logic for SplashScreen.xaml
    /// </summary>
    public partial class SplashScreen : OverlayWindow
    {
        public SplashScreen()
        {
            InitializeComponent();
        }

        private void OverlayWindow_ActualThemeChanged(object sender, RoutedEventArgs e)
        {
            // do something
        }
    }
}
