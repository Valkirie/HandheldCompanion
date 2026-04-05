using HandheldCompanion.Misc;
using HandheldCompanion.ViewModels;
using System;
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

            _vm.FanCurveUpdateRequested += OnFanCurveUpdateRequested;
        }

        private void OnFanCurveUpdateRequested(double[] fanSpeeds)
        {
            Dispatcher.Invoke(() =>
            {
                _vm.SetUpdatingFanCurveUI(true);
                try
                {
                    for (int idx = 0; idx < lvLineSeries.ActualValues.Count; idx++)
                        lvLineSeries.ActualValues[idx] = fanSpeeds[idx];
                }
                finally
                {
                    _vm.SetUpdatingFanCurveUI(false);
                }
            });
        }

        public void SelectionChanged(PowerProfile preset)
        {
            _vm.SelectedPreset = preset;
        }
    }
}
