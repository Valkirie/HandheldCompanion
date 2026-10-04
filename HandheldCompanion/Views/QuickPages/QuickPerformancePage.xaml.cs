using HandheldCompanion.Helpers;
using HandheldCompanion.Managers;
using HandheldCompanion.ViewModels;
using System;
using System.Windows;
using Page = System.Windows.Controls.Page;

namespace HandheldCompanion.Views.QuickPages;

public partial class QuickPerformancePage : Page
{
    private PerformancePageViewModel _vm;
    public QuickPerformancePage()
    {
        Tag = "quickperformance";
        _vm = new PerformancePageViewModel(isQuickTools: true);
        DataContext = _vm;
        InitializeComponent();
        _vm.InitializeViewDependencies(lvc, lvLineSeries, null);
    }

    public void SelectionChanged(Guid guid)
    {
        ((PerformancePageViewModel)DataContext).SelectedPreset = ManagerFactory.powerProfileManager.GetProfile(guid);
    }

    private void Page_Loaded(object sender, RoutedEventArgs e)
    {
        _vm.OnPageLoaded();
        _vm.FanCurveUpdateRequested += OnFanCurveUpdateRequested;
    }

    private void Page_Unloaded(object sender, RoutedEventArgs e)
    {
        _vm.OnPageUnloaded();
        _vm.FanCurveUpdateRequested -= OnFanCurveUpdateRequested;
    }

    private void OnFanCurveUpdateRequested(double[] fanSpeeds)
    {
        UIHelper.TryBeginInvoke(() =>
        {
            if (!IsVisible)
                return;

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

    public void Dispose()
    {
        _vm.OnPageUnloaded();
        _vm.FanCurveUpdateRequested -= OnFanCurveUpdateRequested;
        _vm.Dispose();
    }
}