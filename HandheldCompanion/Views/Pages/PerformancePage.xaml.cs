using HandheldCompanion.Misc;
using HandheldCompanion.ViewModels;
using Page = System.Windows.Controls.Page;

namespace HandheldCompanion.Views.Pages
{
    public partial class PerformancePage : Page
    {
        private PerformancePageViewModel _vm;
        public PerformancePage()
        {
            Tag = "performance";
            _vm = new PerformancePageViewModel(isQuickTools: false);
            DataContext = _vm;
            InitializeComponent();
            _vm.InitializeViewDependencies(lvc, lvLineSeries, PowerProfileSettingsDialog);
        }

        public void SelectionChanged(PowerProfile preset)
        {
            _vm.SelectedPreset = preset;
        }
    }
}
