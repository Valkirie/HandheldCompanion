using HandheldCompanion.ViewModels;

namespace HandheldCompanion.Views.Pages
{
    public partial class ButtonsPage : ILayoutPage
    {
        public ButtonsPage()
        {
            DataContext = new ButtonsPageViewModel();
            InitializeComponent();
        }
    }
}