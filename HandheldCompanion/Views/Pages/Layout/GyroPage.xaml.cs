using HandheldCompanion.ViewModels;

namespace HandheldCompanion.Views.Pages
{
    public partial class GyroPage : ILayoutPage
    {
        public GyroPage()
        {
            DataContext = new GyroPageViewModel();
            InitializeComponent();
        }
    }
}