using HandheldCompanion.Helpers;
using HandheldCompanion.ViewModels;
using System;
using System.Windows;
using System.Windows.Controls;

namespace HandheldCompanion.Views.Pages;

public partial class ActionSettingsPage : Page
{
    private MappingViewModel? _mapping;
    private AxisMappingViewModel? _axisMapping;

    public ActionSettingsPage()
    {
        InitializeComponent();
    }

    public void SetMapping(MappingViewModel mapping)
    {
        DetachAxisMapping();

        _mapping = mapping;
        DataContext = mapping;

        if (mapping is AxisMappingViewModel axisMapping)
        {
            _axisMapping = axisMapping;
            _axisMapping.InitializeViewDependencies(responseCurveChart, responseCurveLineSeries);
            _axisMapping.ResponseCurveUpdateRequested += OnResponseCurveUpdateRequested;
        }
    }

    private void DetachAxisMapping()
    {
        if (_axisMapping is null)
            return;

        _axisMapping.ResponseCurveUpdateRequested -= OnResponseCurveUpdateRequested;
        _axisMapping.ReleaseViewDependencies();
        _axisMapping = null;
    }

    private void OnResponseCurveUpdateRequested(double[] responseCurvePoints)
    {
        UIHelper.TryBeginInvoke(() =>
        {
            if (!IsVisible || _axisMapping is null)
                return;

            _axisMapping.SetUpdatingResponseCurveUI(true);
            try
            {
                int count = Math.Min(responseCurveLineSeries.ActualValues.Count, responseCurvePoints.Length);
                for (int idx = 0; idx < count; idx++)
                    responseCurveLineSeries.ActualValues[idx] = responseCurvePoints[idx];
            }
            finally
            {
                _axisMapping.SetUpdatingResponseCurveUI(false);
            }
        });
    }
}

