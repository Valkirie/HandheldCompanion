using HandheldCompanion.Devices;
using HandheldCompanion.Inputs;
using HandheldCompanion.Managers;
using LiveCharts;
using LiveCharts.Wpf;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;

namespace HandheldCompanion.ViewModels
{
    public class SettingsMode0ViewModel : BaseViewModel
    {
        private const double Epsilon = 0.0001d;
        private const ButtonFlags gyroButtonFlags = ButtonFlags.HOTKEY_GYRO_AIMING;
        private CartesianChart? _motionSensitivityChart;
        private LineSeries? _motionSensitivityLineSeries;
        private AxisSection? _motionSensitivityIndicator;
        private bool _isDraggingSensitivityPoint;
        private bool _sensitivityCurveDirty;
        private int _sensitivityDragIndex = -1;

        public ObservableCollection<HotkeyViewModel> HotkeysList { get; set; } = [];
        public Func<double, string> ResponseCurveAxisYFormatter { get; } = v => Math.Round(v, 1).ToString("0.0");
        public event Action<double[]>? MotionSensitivityCurveChanged;

        public SettingsMode0ViewModel()
        {
            // Enable thread-safe access to the collection
            BindingOperations.EnableCollectionSynchronization(HotkeysList, _collectionLock);

            ManagerFactory.hotkeysManager.Updated += HotkeysManager_Updated;
            InputsManager.StartedListening += InputsManager_StartedListening;
            InputsManager.StoppedListening += InputsManager_StoppedListening;
        }

        public void InitializeMotionChart(CartesianChart chart, LineSeries lineSeries, AxisSection indicator)
        {
            ReleaseMotionChart();

            _motionSensitivityChart = chart;
            _motionSensitivityLineSeries = lineSeries;
            _motionSensitivityIndicator = indicator;

            chart.DataClick += MotionSensitivityChart_DataClick;
            chart.MouseLeave += MotionSensitivityChart_MouseLeave;
            chart.MouseMove += MotionSensitivityChart_MouseMove;
            chart.MouseUp += MotionSensitivityChart_MouseUp;
            chart.TouchMove += MotionSensitivityChart_TouchMove;
            chart.PreviewTouchDown += MotionSensitivityChart_PreviewTouchDown;
        }

        public void ReleaseMotionChart()
        {
            if (_motionSensitivityChart is not null)
            {
                _motionSensitivityChart.DataClick -= MotionSensitivityChart_DataClick;
                _motionSensitivityChart.MouseLeave -= MotionSensitivityChart_MouseLeave;
                _motionSensitivityChart.MouseMove -= MotionSensitivityChart_MouseMove;
                _motionSensitivityChart.MouseUp -= MotionSensitivityChart_MouseUp;
                _motionSensitivityChart.TouchMove -= MotionSensitivityChart_TouchMove;
                _motionSensitivityChart.PreviewTouchDown -= MotionSensitivityChart_PreviewTouchDown;
            }

            _motionSensitivityChart = null;
            _motionSensitivityLineSeries = null;
            _motionSensitivityIndicator = null;
            _isDraggingSensitivityPoint = false;
            _sensitivityCurveDirty = false;
            _sensitivityDragIndex = -1;
        }

        public void SetMotionSensitivityCurve(double[] curve)
        {
            if (_motionSensitivityLineSeries is null)
                return;

            int count = Math.Min(_motionSensitivityLineSeries.ActualValues.Count, curve.Length);
            for (int idx = 0; idx < count; idx++)
                _motionSensitivityLineSeries.ActualValues[idx] = curve[idx];
        }

        public void UpdateMotionSensitivityIndicator(float value)
        {
            if (_motionSensitivityIndicator is null)
                return;

            double gyroThreshold = IDevice.GetCurrent().GamepadMotion.GetCalibration().GetGyroThreshold();
            double normalizedValue = gyroThreshold > 0d ? value / gyroThreshold : 0d;
            _motionSensitivityIndicator.Value = Math.Clamp(normalizedValue, 0d, 1d) * (Profile.MotionSensitivityArraySize - 1);
        }

        public void ApplyDefaultMotionSensitivityPreset() => ApplyMotionSensitivityPreset(_ => 0.5d);

        public void ApplyAggressiveMotionSensitivityPreset()
        {
            double factor = 24d / 49d;
            ApplyMotionSensitivityPreset(position => -Math.Sqrt(position * factor) + 0.85d);
        }

        public void ApplyPreciseMotionSensitivityPreset()
        {
            double factor = 12d / 49d;
            ApplyMotionSensitivityPreset(position => Math.Sqrt(position * factor) + 0.25d - factor * position);
        }

        private static int ClampSensitivityIndex(double value)
        {
            return Math.Clamp((int)Math.Round(value), 0, Profile.MotionSensitivityArraySize - 1);
        }

        private static double ClampSensitivityValue(double value)
        {
            return Math.Clamp(value, 0d, 1d);
        }

        private void ApplyMotionSensitivityPreset(Func<double, double> getValue)
        {
            double[] curve = new double[Profile.MotionSensitivityArraySize];
            for (int idx = 0; idx < curve.Length; idx++)
            {
                double position = idx / (double)(curve.Length - 1);
                curve[idx] = ClampSensitivityValue(getValue(position));
            }

            SetMotionSensitivityCurve(curve);
            MotionSensitivityCurveChanged?.Invoke(curve);
        }

        private void MoveSensitivityPoint(Point point)
        {
            if (!_isDraggingSensitivityPoint || _sensitivityDragIndex < 0 || _motionSensitivityLineSeries is null)
                return;

            double value = ClampSensitivityValue(point.Y);
            double currentValue = Convert.ToDouble(_motionSensitivityLineSeries.ActualValues[_sensitivityDragIndex] ?? 0.5d);
            if (Math.Abs(value - currentValue) < Epsilon)
                return;

            _motionSensitivityLineSeries.ActualValues[_sensitivityDragIndex] = value;
            _sensitivityCurveDirty = true;
        }

        private void EndSensitivityDrag()
        {
            if (!_isDraggingSensitivityPoint)
                return;

            _isDraggingSensitivityPoint = false;
            _sensitivityDragIndex = -1;

            if (_motionSensitivityChart is not null && Mouse.Captured == _motionSensitivityChart)
                _motionSensitivityChart.ReleaseMouseCapture();

            if (!_sensitivityCurveDirty || _motionSensitivityLineSeries is null)
                return;

            double[] curve = new double[Profile.MotionSensitivityArraySize];
            for (int idx = 0; idx < curve.Length; idx++)
                curve[idx] = Convert.ToDouble(_motionSensitivityLineSeries.ActualValues[idx] ?? 0.5d);

            _sensitivityCurveDirty = false;
            MotionSensitivityCurveChanged?.Invoke(curve);
        }

        private void MotionSensitivityChart_DataClick(object sender, ChartPoint chartPoint)
        {
            if (chartPoint is null || _motionSensitivityChart is null || _motionSensitivityLineSeries is null || _motionSensitivityLineSeries.ActualValues.Count == 0)
                return;

            Point point = _motionSensitivityChart.ConvertToChartValues(Mouse.GetPosition(_motionSensitivityChart));
            _sensitivityDragIndex = ClampSensitivityIndex(point.X);
            _isDraggingSensitivityPoint = true;
            _motionSensitivityChart.CaptureMouse();
        }

        private void MotionSensitivityChart_MouseMove(object sender, MouseEventArgs e)
        {
            if (_motionSensitivityChart is null)
                return;

            MoveSensitivityPoint(_motionSensitivityChart.ConvertToChartValues(e.GetPosition(_motionSensitivityChart)));
            e.Handled = true;
        }

        private void MotionSensitivityChart_TouchMove(object? sender, TouchEventArgs e)
        {
            if (_motionSensitivityChart is null)
                return;

            MoveSensitivityPoint(_motionSensitivityChart.ConvertToChartValues(e.GetTouchPoint(_motionSensitivityChart).Position));
            e.Handled = true;
        }

        private void MotionSensitivityChart_PreviewTouchDown(object? sender, TouchEventArgs e)
        {
            e.Handled = true;
        }

        private void MotionSensitivityChart_MouseUp(object sender, MouseButtonEventArgs e)
        {
            EndSensitivityDrag();
        }

        private void MotionSensitivityChart_MouseLeave(object sender, MouseEventArgs e)
        {
            EndSensitivityDrag();
        }

        private void HotkeysManager_Updated(Hotkey hotkey)
        {
            if (hotkey.ButtonFlags != gyroButtonFlags)
                return;

            if (!Monitor.TryEnter(_collectionLock, TimeSpan.FromSeconds(2)))
                return;

            try
            {
                HotkeyViewModel? foundHotkey = HotkeysList.FirstOrDefault(p => p.Hotkey.ButtonFlags == hotkey.ButtonFlags);
                if (foundHotkey is null)
                    HotkeysList.Add(new HotkeyViewModel(hotkey));
                else
                    foundHotkey.Hotkey = hotkey;
            }
            finally
            {
                Monitor.Exit(_collectionLock);
            }
        }

        private void InputsManager_StartedListening(ButtonFlags buttonFlags, InputsChordTarget chordTarget)
        {
            HotkeyViewModel? hotkeyViewModel = HotkeysList.FirstOrDefault(h => h.Hotkey.ButtonFlags == buttonFlags);
            hotkeyViewModel?.SetListening(true, chordTarget);
        }

        private void InputsManager_StoppedListening(ButtonFlags buttonFlags, InputsChord storedChord)
        {
            HotkeyViewModel? hotkeyViewModel = HotkeysList.FirstOrDefault(h => h.Hotkey.ButtonFlags == buttonFlags);
            hotkeyViewModel?.SetListening(false, storedChord.chordTarget);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                ReleaseMotionChart();
                ManagerFactory.hotkeysManager.Updated -= HotkeysManager_Updated;
                InputsManager.StartedListening -= InputsManager_StartedListening;
                InputsManager.StoppedListening -= InputsManager_StoppedListening;
            }

            base.Dispose(disposing);
        }
    }
}
