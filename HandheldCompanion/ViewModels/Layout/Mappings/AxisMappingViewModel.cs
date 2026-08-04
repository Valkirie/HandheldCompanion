using GregsStack.InputSimulatorStandard.Native;
using HandheldCompanion.Actions;
using HandheldCompanion.Controllers;
using HandheldCompanion.Inputs;
using HandheldCompanion.Managers;
using HandheldCompanion.Utils;
using HandheldCompanion.Views;
using LiveCharts;
using LiveCharts.Wpf;
using SharpDX.XInput;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Numerics;
using System.Windows;
using System.Windows.Input;

namespace HandheldCompanion.ViewModels
{
    public class AxisMappingViewModel : MappingViewModel
    {
        #region Axis Action Properties

        #region Axis2Button
        public int Axis2ButtonDirection
        {
            get => (int)((Action is IActions iActions) ? iActions.motionDirection : 0);
            set
            {
                if (Action is IActions iActions && value != Axis2ButtonDirection)
                {
                    iActions.motionDirection = (DeflectionDirection)value;
                    OnPropertyChanged(nameof(Axis2ButtonDirection));

                    // Cascade notifications to dependent properties
                    OnPropertyChanged(nameof(IsLeft));
                    OnPropertyChanged(nameof(IsRight));
                    OnPropertyChanged(nameof(IsUp));
                    OnPropertyChanged(nameof(IsDown));
                }
            }
        }

        public override bool IsLeft
        {
            get => ((DeflectionDirection)Axis2ButtonDirection).HasFlag(DeflectionDirection.Left);
            set
            {
                if (value != IsLeft)
                {
                    Axis2ButtonDirection = value
                        ? Axis2ButtonDirection | (int)DeflectionDirection.Left
                        : Axis2ButtonDirection & ~(int)DeflectionDirection.Left;
                }
            }
        }

        public override bool IsRight
        {
            get => ((DeflectionDirection)Axis2ButtonDirection).HasFlag(DeflectionDirection.Right);
            set
            {
                if (value != IsRight)
                {
                    Axis2ButtonDirection = value
                        ? Axis2ButtonDirection | (int)DeflectionDirection.Right
                        : Axis2ButtonDirection & ~(int)DeflectionDirection.Right;
                }
            }
        }

        public override bool IsUp
        {
            get => ((DeflectionDirection)Axis2ButtonDirection).HasFlag(DeflectionDirection.Up);
            set
            {
                if (value != IsUp)
                {
                    Axis2ButtonDirection = value
                        ? Axis2ButtonDirection | (int)DeflectionDirection.Up
                        : Axis2ButtonDirection & ~(int)DeflectionDirection.Up;
                }
            }
        }

        public override bool IsDown
        {
            get => ((DeflectionDirection)Axis2ButtonDirection).HasFlag(DeflectionDirection.Down);
            set
            {
                if (value != IsDown)
                {
                    Axis2ButtonDirection = value
                        ? Axis2ButtonDirection | (int)DeflectionDirection.Down
                        : Axis2ButtonDirection & ~(int)DeflectionDirection.Down;
                }
            }
        }
        #endregion

        public override int Axis2AxisInnerDeadzone
        {
            get => (Action is AxisActions axisAction) ? axisAction.AxisDeadZoneInner : (Action is TriggerActions triggerAction) ? triggerAction.AxisDeadZoneInner : 0;
            set
            {
                if (Action is AxisActions axisAction && value != Axis2AxisInnerDeadzone)
                {
                    axisAction.AxisDeadZoneInner = value;
                    OnPropertyChanged(nameof(Axis2AxisInnerDeadzone));
                    OnPropertyChanged(nameof(AxisVisualizerInnerDeadzoneSize));
                }
                else if (Action is TriggerActions triggerAction && value != Axis2AxisInnerDeadzone)
                {
                    triggerAction.AxisDeadZoneInner = value;
                    OnPropertyChanged(nameof(Axis2AxisInnerDeadzone));
                    OnPropertyChanged(nameof(AxisVisualizerInnerDeadzoneSize));
                }
            }
        }

        public override int Axis2AxisOuterDeadzone
        {
            get => (Action is AxisActions axisAction) ? axisAction.AxisDeadZoneOuter : (Action is TriggerActions triggerAction) ? triggerAction.AxisDeadZoneOuter : 0;
            set
            {
                if (Action is AxisActions axisAction && value != Axis2AxisOuterDeadzone)
                {
                    axisAction.AxisDeadZoneOuter = value;
                    OnPropertyChanged(nameof(Axis2AxisOuterDeadzone));
                    OnPropertyChanged(nameof(AxisVisualizerOuterDeadzoneSize));
                }
                else if (Action is TriggerActions triggerAction && value != Axis2AxisOuterDeadzone)
                {
                    triggerAction.AxisDeadZoneOuter = value;
                    OnPropertyChanged(nameof(Axis2AxisOuterDeadzone));
                    OnPropertyChanged(nameof(AxisVisualizerOuterDeadzoneSize));
                }
            }
        }

        public override int Axis2AxisAntiDeadzone
        {
            get => (Action is AxisActions axisAction) ? axisAction.AxisAntiDeadZone : (Action is TriggerActions triggerAction) ? triggerAction.AxisAntiDeadZone : 0;
            set
            {
                if (Action is AxisActions axisAction && value != Axis2AxisAntiDeadzone)
                {
                    axisAction.AxisAntiDeadZone = value;
                    OnPropertyChanged(nameof(Axis2AxisAntiDeadzone));
                    OnPropertyChanged(nameof(AxisVisualizerAntiDeadzoneSize));
                }
                else if (Action is TriggerActions triggerAction && value != Axis2AxisAntiDeadzone)
                {
                    triggerAction.AxisAntiDeadZone = value;
                    OnPropertyChanged(nameof(Axis2AxisAntiDeadzone));
                    OnPropertyChanged(nameof(AxisVisualizerAntiDeadzoneSize));
                }
            }
        }

        public override Visibility AxisResponseCurveVisibility
        {
            get => Action is AxisActions axisAction && axisAction.actionType == ActionType.Joystick ? Visibility.Visible : Visibility.Collapsed;
        }

        public override int Axis2AxisOutputShapeIndex
        {
            get => (Action is AxisActions axisAction) ? (int)axisAction.OutputShape : 0;
            set
            {
                if (Action is AxisActions axisAction && value != Axis2AxisOutputShapeIndex)
                {
                    axisAction.OutputShape = (OutputShape)value;
                    OnPropertyChanged(nameof(Axis2AxisOutputShapeIndex));
                }
            }
        }

        public override bool Axis2AxisInvertHorizontal
        {
            get => (Action is AxisActions axisAction) ? axisAction.InvertHorizontal : false;
            set
            {
                if (Action is AxisActions axisAction && value != Axis2AxisInvertHorizontal)
                {
                    axisAction.InvertHorizontal = value;
                    OnPropertyChanged(nameof(Axis2AxisInvertHorizontal));
                }
            }
        }

        public override bool Axis2AxisInvertVertical
        {
            get => (Action is AxisActions axisAction) ? axisAction.InvertVertical : false;
            set
            {
                if (Action is AxisActions axisAction && value != Axis2AxisInvertVertical)
                {
                    axisAction.InvertVertical = value;
                    OnPropertyChanged(nameof(Axis2AxisInvertVertical));
                }
            }
        }

        #endregion

        #region Mouse Action Properties

        public override int Axis2MousePointerSpeed
        {
            get => (Action is MouseActions mouseAction) ? mouseAction.Sensivity : 0;
            set
            {
                if (Action is MouseActions mouseAction && value != Axis2MousePointerSpeed)
                {
                    mouseAction.Sensivity = value;
                    OnPropertyChanged(nameof(Axis2MousePointerSpeed));
                }
            }
        }

        public override int Axis2MouseDeadzone
        {
            get => (Action is MouseActions mouseAction) ? mouseAction.Deadzone : 0;
            set
            {
                if (Action is MouseActions mouseAction && value != Axis2MouseDeadzone)
                {
                    mouseAction.Deadzone = value;
                    OnPropertyChanged(nameof(Axis2MouseDeadzone));
                }
            }
        }

        public override float Axis2MouseAcceleration
        {
            get => (Action is MouseActions mouseAction) ? mouseAction.Acceleration : 0;
            set
            {
                if (Action is MouseActions mouseAction && value != Axis2MouseAcceleration)
                {
                    mouseAction.Acceleration = value;
                    OnPropertyChanged(nameof(Axis2MouseAcceleration));
                }
            }
        }

        public override bool Axis2MouseFiltering
        {
            get => (Action is MouseActions mouseAction) && mouseAction.Filtering;
            set
            {
                if (Action is MouseActions mouseAction && value != Axis2MouseFiltering)
                {
                    mouseAction.Filtering = value;
                    OnPropertyChanged(nameof(Axis2MouseFiltering));
                }
            }
        }

        public override float Axis2MouseFilterCutoff
        {
            get => (Action is MouseActions mouseAction) ? mouseAction.FilterCutoff : 0;
            set
            {
                if (Action is MouseActions mouseAction && value != Axis2MouseFilterCutoff)
                {
                    mouseAction.FilterCutoff = value;
                    OnPropertyChanged(nameof(Axis2MouseFilterCutoff));
                }
            }
        }

        public override double Axis2MouseToX
        {
            get => (Action is MouseActions mouseAction) ? mouseAction.MoveToX : 0;
            set
            {
                if (Action is MouseActions mouseAction && value != Axis2MouseToX)
                {
                    mouseAction.MoveToX = value is double.NaN ? 0 : value;
                    OnPropertyChanged(nameof(Axis2MouseToX));
                }
            }
        }

        public override double Axis2MouseToY
        {
            get => (Action is MouseActions mouseAction) ? mouseAction.MoveToY : 0;
            set
            {
                if (Action is MouseActions mouseAction && value != Axis2MouseToY)
                {
                    mouseAction.MoveToY = value is double.NaN ? 0 : value;
                    OnPropertyChanged(nameof(Axis2MouseToY));
                }
            }
        }

        public override bool Axis2MouseRestore
        {
            get => (Action is MouseActions mouseAction) ? mouseAction.MoveToPrevious : false;
            set
            {
                if (Action is MouseActions mouseAction && value != Axis2MouseRestore)
                {
                    mouseAction.MoveToPrevious = value;
                    OnPropertyChanged(nameof(Axis2MouseRestore));
                }
            }
        }

        public override Visibility Axis2MouseTo
        {
            get
            {
                ActionType currentActionType = (ActionType)ActionTypeIndex;
                if (currentActionType == ActionType.Mouse && SelectedTarget != null)
                {
                    if (SelectedTarget.Tag is not MouseActionsType mouseAction)
                        return Visibility.Collapsed;

                    return mouseAction == MouseActionsType.MoveTo ? Visibility.Visible : Visibility.Collapsed;
                }

                return Visibility.Collapsed;
            }
        }

        public override void OnPropertyChanged(string? propertyName)
        {
            switch (propertyName)
            {
                case "SelectedTarget":
                case "ActionTypeIndex":
                    OnPropertyChanged(nameof(Axis2MouseVisibility));
                    OnPropertyChanged(nameof(Axis2ButtonVisibility));
                    OnPropertyChanged(nameof(Axis2MouseTo));
                    OnPropertyChanged(nameof(Axis2TouchpadVisibility));
                    OnPropertyChanged(nameof(Axis2JoystickVisibility));
                    OnPropertyChanged(nameof(MouseSettingsSectionVisibility));
                    OnPropertyChanged(nameof(AxisSettingsSectionVisibility));
                    OnPropertyChanged(nameof(TriggerSettingsSectionVisibility));
                    OnPropertyChanged(nameof(AxisDirectionVisibility));
                    OnPropertyChanged(nameof(AxisThresholdVisibility));
                    OnPropertyChanged(nameof(GeneralActionVisibility));
                    OnPropertyChanged(nameof(AxisInvertVisibility));
                    break;
            }

            base.OnPropertyChanged(propertyName);
        }

        #endregion

        private AxisStackViewModel _parentStack;
        public AxisStackViewModel ParentStack => _parentStack;

        public ICommand ButtonCommand { get; private set; }
        public ICommand OpenSettingsCommand { get; private set; }
        public ICommand ResetResponseCurveCommand { get; private set; }

        private CartesianChart? _responseCurveGraph;
        private LineSeries? _responseCurveLineSeries;
        private bool _updatingResponseCurveUI;
        private bool _responseCurveDirty;
        private bool _isDraggingResponseCurve;
        private int _responseCurveDragIndex = -1;
        private const double Epsilon = 0.0001;

        public Func<double, string> ResponseCurveAxisYFormatter { get; } = v => v.ToString("0.0");

        public event Action<double[]>? ResponseCurveUpdateRequested;

        public override Visibility Axis2TouchpadVisibility => Axis2MouseVisibility == Visibility.Visible && TouchpadVisibility == Visibility.Visible ? Visibility.Visible : Visibility.Collapsed;
        public override Visibility Axis2JoystickVisibility => Axis2MouseVisibility == Visibility.Visible && JoystickVisibility == Visibility.Visible ? Visibility.Visible : Visibility.Collapsed;

        public override bool IsAxisMapping => true;

        // Axis Direction and Threshold are only visible when converting Axis to Button
        public override Visibility AxisDirectionVisibility => Axis2ButtonVisibility;
        public override Visibility AxisThresholdVisibility => Axis2ButtonVisibility;

        // Axis invert properties should only be visible for Axis -> Joystick mappings
        public override Visibility AxisInvertVisibility
        {
            get
            {
                ActionType currentActionType = (ActionType)ActionTypeIndex;
                return currentActionType == ActionType.Joystick ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        public override Visibility AxisVisualizerVisibility => AxisInvertVisibility;
        public override double AxisVisualizerDotX => 0.0d;
        public override double AxisVisualizerDotY => 0.0d;
        public override double AxisVisualizerDotTranslateX => 0.0d;
        public override double AxisVisualizerDotTranslateY => 0.0d;
        public override double AxisVisualizerInnerDeadzoneSize => Axis2AxisInnerDeadzone * 2.0d;
        public override double AxisVisualizerOuterDeadzoneSize => Math.Max(0.0d, 200.0d - Axis2AxisOuterDeadzone * 2.0d);
        public override double AxisVisualizerAntiDeadzoneSize => Axis2AxisAntiDeadzone * 2.0d;

        public Visibility TouchpadVisibility => _parentStack._touchpad ? Visibility.Visible : Visibility.Collapsed;
        public Visibility JoystickVisibility => _parentStack._touchpad ? Visibility.Collapsed : Visibility.Visible;

        public AxisMappingViewModel(AxisStackViewModel parentStack, AxisLayoutFlags value) : base(value)
        {
            _parentStack = parentStack;

            ButtonCommand = new DelegateCommand(() =>
            {
                if (Action is not null) Delete();
                _parentStack.RemoveMapping(this);
            });

            OpenSettingsCommand = new DelegateCommand(() =>
            {
                // Navigate to LayoutItemPage
                if (MainWindow.layoutItemPage is not null)
                {
                    MainWindow.layoutItemPage.SetMapping(this);
                    MainWindow.NavView_Navigate(MainWindow.layoutItemPage);
                }
            });

            ResetResponseCurveCommand = new DelegateCommand(ResetResponseCurve);
        }

        private void ResetResponseCurve()
        {
            if (Action is not AxisActions axisAction)
                return;

            axisAction.ResponseCurvePoints = new List<Vector2>
            {
                new Vector2(0.0f, 0.0f),
                new Vector2(0.2f, 0.2f),
                new Vector2(0.4f, 0.4f),
                new Vector2(0.6f, 0.6f),
                new Vector2(0.8f, 0.8f),
                new Vector2(1.0f, 1.0f)
            };

            _responseCurveDirty = false;
            PushResponseCurveToView();
            _parentStack.UpdateFromMapping();
        }

        public void InitializeViewDependencies(CartesianChart responseCurveGraph, LineSeries responseCurveLineSeries)
        {
            ReleaseViewDependencies();

            _responseCurveGraph = responseCurveGraph;
            _responseCurveLineSeries = responseCurveLineSeries;

            _responseCurveLineSeries.ActualValues.CollectionChanged += ResponseCurveActualValues_CollectionChanged;
            _responseCurveGraph.DataClick += ResponseCurveChartOnDataClick;
            _responseCurveGraph.MouseLeave += ResponseCurveChartMouseLeave;
            _responseCurveGraph.MouseMove += ResponseCurveChartMouseMove;
            _responseCurveGraph.MouseUp += ResponseCurveChartMouseUp;
            _responseCurveGraph.TouchMove += ResponseCurveChartTouchMove;
            _responseCurveGraph.PreviewTouchDown += ResponseCurveGraph_PreviewTouchDown;

            PushResponseCurveToView();
        }

        public void ReleaseViewDependencies()
        {
            if (_responseCurveLineSeries is not null)
                _responseCurveLineSeries.ActualValues.CollectionChanged -= ResponseCurveActualValues_CollectionChanged;

            if (_responseCurveGraph is not null)
            {
                _responseCurveGraph.DataClick -= ResponseCurveChartOnDataClick;
                _responseCurveGraph.MouseLeave -= ResponseCurveChartMouseLeave;
                _responseCurveGraph.MouseMove -= ResponseCurveChartMouseMove;
                _responseCurveGraph.MouseUp -= ResponseCurveChartMouseUp;
                _responseCurveGraph.TouchMove -= ResponseCurveChartTouchMove;
                _responseCurveGraph.PreviewTouchDown -= ResponseCurveGraph_PreviewTouchDown;
            }

            _responseCurveGraph = null;
            _responseCurveLineSeries = null;
            _updatingResponseCurveUI = false;
            _responseCurveDirty = false;
            _isDraggingResponseCurve = false;
            _responseCurveDragIndex = -1;
        }

        public void SetUpdatingResponseCurveUI(bool value) => _updatingResponseCurveUI = value;

        private static int ClampIndex(double x, int maxIndex)
        {
            int idx = (int)Math.Round(x);
            if (idx < 0) return 0;
            if (idx > maxIndex) return maxIndex;
            return idx;
        }

        private static double Clamp01(double y)
        {
            if (y < 0d) return 0d;
            if (y > 1d) return 1d;
            return y;
        }

        private void ResponseCurveGraph_PreviewTouchDown(object? sender, System.Windows.Input.TouchEventArgs e)
        {
            e.Handled = true;
        }

        private void ResponseCurveActualValues_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (_updatingResponseCurveUI || _responseCurveLineSeries is null || Action is not AxisActions axisAction)
                return;

            if (_isDraggingResponseCurve)
            {
                _responseCurveDirty = true;
                return;
            }

            CommitResponseCurveFromChart(axisAction, submitMapping: false);
        }

        private void CommitResponseCurveFromChart(AxisActions axisAction, bool submitMapping)
        {
            if (_responseCurveLineSeries is null)
                return;

            List<Vector2> responseCurvePoints = axisAction.ResponseCurvePoints;
            int count = Math.Min(_responseCurveLineSeries.ActualValues.Count, responseCurvePoints.Count);
            bool changed = false;

            for (int idx = 0; idx < count; idx++)
            {
                double value = Convert.ToDouble(_responseCurveLineSeries.ActualValues[idx] ?? 0.0d);
                Vector2 point = responseCurvePoints[idx];

                if (Math.Abs(point.Y - value) < Epsilon)
                    continue;

                responseCurvePoints[idx] = new Vector2(point.X, (float)value);
                changed = true;
            }

            _responseCurveDirty = false;

            if (changed)
            {
                ResponseCurveUpdateRequested?.Invoke(responseCurvePoints.Select(point => (double)point.Y).ToArray());

                if (submitMapping)
                    _parentStack.UpdateFromMapping();
            }
        }

        private void PushResponseCurveToView()
        {
            if (_responseCurveLineSeries is null || Action is not AxisActions axisAction)
                return;

            if (_responseCurveLineSeries.ActualValues.Count == 0)
                return;

            _updatingResponseCurveUI = true;
            try
            {
                int count = Math.Min(_responseCurveLineSeries.ActualValues.Count, axisAction.ResponseCurvePoints.Count);
                for (int idx = 0; idx < count; idx++)
                    _responseCurveLineSeries.ActualValues[idx] = (double)axisAction.ResponseCurvePoints[idx].Y;

                ResponseCurveUpdateRequested?.Invoke(axisAction.ResponseCurvePoints.Select(point => (double)point.Y).ToArray());
            }
            finally
            {
                _updatingResponseCurveUI = false;
            }
        }

        private void ChartMovePoint(Point p)
        {
            if (_responseCurveLineSeries is null)
                return;

            int idx = ClampIndex(p.X, _responseCurveLineSeries.ActualValues.Count - 1);

            if (!_isDraggingResponseCurve || _responseCurveDragIndex < 0)
                return;

            double newY = Clamp01(p.Y);
            double currentY = Convert.ToDouble(_responseCurveLineSeries.ActualValues[_responseCurveDragIndex] ?? 0.0d);
            if (Math.Abs(newY - currentY) < Epsilon)
                return;

            _responseCurveLineSeries.ActualValues[_responseCurveDragIndex] = newY;

            double carry = newY;
            for (int i = _responseCurveDragIndex + 1; i < _responseCurveLineSeries.ActualValues.Count; i++)
            {
                double yi = Convert.ToDouble(_responseCurveLineSeries.ActualValues[i] ?? 0.0d);
                if (yi + Epsilon < carry) _responseCurveLineSeries.ActualValues[i] = carry;
                else carry = yi;
            }

            carry = newY;
            for (int i = _responseCurveDragIndex - 1; i >= 0; i--)
            {
                double yi = Convert.ToDouble(_responseCurveLineSeries.ActualValues[i] ?? 0.0d);
                if (yi - Epsilon > carry) _responseCurveLineSeries.ActualValues[i] = carry;
                else carry = yi;
            }
        }

        private void ResponseCurveChartMouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (_responseCurveGraph is null)
                return;

            ChartMovePoint(_responseCurveGraph.ConvertToChartValues(e.GetPosition(_responseCurveGraph)));
            e.Handled = true;
        }

        private void ResponseCurveChartTouchMove(object? sender, System.Windows.Input.TouchEventArgs e)
        {
            if (_responseCurveGraph is null)
                return;

            ChartMovePoint(_responseCurveGraph.ConvertToChartValues(e.GetTouchPoint(_responseCurveGraph).Position));
            e.Handled = true;
        }

        private void ResponseCurveChartMouseUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            EndResponseCurveDrag();
        }

        private void ResponseCurveChartMouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
        {
            EndResponseCurveDrag();
        }

        private void EndResponseCurveDrag()
        {
            if (_responseCurveGraph is null || Action is not AxisActions axisAction)
                return;

            if (!_isDraggingResponseCurve)
                return;

            _isDraggingResponseCurve = false;
            _responseCurveDragIndex = -1;

            if (_responseCurveDirty)
                CommitResponseCurveFromChart(axisAction, submitMapping: true);

            if (Mouse.Captured == _responseCurveGraph)
                _responseCurveGraph.ReleaseMouseCapture();
        }

        private void ResponseCurveChartOnDataClick(object sender, ChartPoint chartPoint)
        {
            if (chartPoint == null || _responseCurveGraph is null || _responseCurveLineSeries is null || _responseCurveLineSeries.ActualValues.Count == 0)
                return;

            Point p = _responseCurveGraph.ConvertToChartValues(Mouse.GetPosition(_responseCurveGraph));
            _responseCurveDragIndex = ClampIndex(p.X, _responseCurveLineSeries.ActualValues.Count - 1);
            _isDraggingResponseCurve = true;
            _responseCurveGraph.CaptureMouse();
        }

        private void PushResponseCurveToViewIfNeeded()
        {
            if (_responseCurveLineSeries is null || Action is not AxisActions axisAction)
                return;

            if (_responseCurveLineSeries.ActualValues.Count == 0)
                return;

            if (_updatingResponseCurveUI)
                return;

            PushResponseCurveToView();
        }

        protected override void ActionTypeChanged(ActionType? newActionType = null)
        {
            var actionType = newActionType ?? (ActionType)ActionTypeIndex;
            if (actionType == ActionType.Disabled)
            {
                IsSupported = true;
                if (Action is not null) Delete();
                SelectedTarget = null;
                OnPropertyChanged(string.Empty);
                return;
            }

            // get current controller
            IController controller = ControllerManager.GetDefault(true);

            // Build Targets
            List<MappingTargetViewModel> targets = new List<MappingTargetViewModel>();

            if (actionType == ActionType.Joystick)
            {
                bool preserveMissingTarget = Action is AxisActions;
                if (!preserveMissingTarget)
                {
                    Action = new AxisActions()
                    {
                        ShiftSlot = ShiftSlot.Any,
                        ShiftMatchAny = false
                    };
                }

                MappingTargetViewModel? matchingTargetVm = null;
                foreach (var axis in controller.GetTargetAxis())
                {
                    var mappingTargetVm = CreateTarget(axis, controller.GetAxisName(axis));
                    targets.Add(mappingTargetVm);

                    if (axis == ((AxisActions)Action).Axis)
                        matchingTargetVm = mappingTargetVm;
                }

                if (matchingTargetVm is null && preserveMissingTarget)
                {
                    matchingTargetVm = CreateUnsupportedTarget(((AxisActions)Action).Axis,
                        controller.GetAxisName(((AxisActions)Action).Axis));
                    targets.Add(matchingTargetVm);
                }

                ReplaceTargets(targets, matchingTargetVm);

                PushResponseCurveToViewIfNeeded();
            }
            else if (actionType == ActionType.Button)
            {
                bool preserveMissingTarget = Action is ButtonActions;
                if (!preserveMissingTarget)
                {
                    Action = new ButtonActions()
                    {
                        motionThreshold = Gamepad.LeftThumbDeadZone,
                        ShiftSlot = ShiftSlot.Any,
                        ShiftMatchAny = false
                    };
                }

                MappingTargetViewModel? matchingTargetVm = null;
                foreach (var button in controller.GetTargetButtons())
                {
                    var mappingTargetVm = CreateTarget(button, controller.GetButtonName(button));
                    targets.Add(mappingTargetVm);

                    if (button == ((ButtonActions)Action).Button)
                        matchingTargetVm = mappingTargetVm;
                }

                if (matchingTargetVm is null && preserveMissingTarget)
                {
                    matchingTargetVm = CreateUnsupportedTarget(((ButtonActions)Action).Button,
                        controller.GetButtonName(((ButtonActions)Action).Button));
                    targets.Add(matchingTargetVm);
                }

                ReplaceTargets(targets, matchingTargetVm);
            }
            else if (actionType == ActionType.Keyboard)
            {
                if (Action is null || Action is not KeyboardActions)
                {
                    Action = new KeyboardActions
                    {
                        motionThreshold = Gamepad.LeftThumbDeadZone,
                        Modifiers = ModifierSet.None,
                        ShiftSlot = ShiftSlot.Any,
                        ShiftMatchAny = false
                    };
                }

                targets.AddRange(_keyboardKeysTargets);
                ReplaceTargets(targets, _keyboardKeysTargets.FirstOrDefault(e => Equals(e.Tag, ((KeyboardActions)Action).Key)));
            }
            else if (actionType == ActionType.Mouse)
            {
                if (Action is null || Action is not MouseActions)
                {
                    Action = new MouseActions
                    {
                        motionThreshold = Gamepad.LeftThumbDeadZone,
                        Modifiers = ModifierSet.None,
                        ShiftSlot = ShiftSlot.Any,
                        ShiftMatchAny = false
                    };
                }

                MappingTargetViewModel? matchingTargetVm = null;
                foreach (var mouseType in Enum.GetValues<MouseActionsType>())
                {
                    var mappingTargetVm = new MappingTargetViewModel
                    {
                        Tag = mouseType,
                        Content = EnumUtils.GetDescriptionFromEnumValue(mouseType)
                    };
                    targets.Add(mappingTargetVm);

                    if (mouseType == ((MouseActions)Action).MouseType)
                        matchingTargetVm = mappingTargetVm;
                }

                // Update list and selected target
                lock (_collectionLock)
                {
                    Targets.Clear();
                    foreach (var t in targets)
                        Targets.Add(t);
                }
                SelectedTarget = matchingTargetVm ?? Targets.First();
            }
            else if (actionType == ActionType.Inherit)
            {
                if (Action is null || Action is not InheritActions)
                {
                    Action = new InheritActions();
                }

                // Update list and selected target
                Targets.Clear();
            }

            // Refresh mapping
            OnPropertyChanged(string.Empty);
        }

        protected override void TargetTypeChanged()
        {
            if (Action is null || SelectedTarget is null)
                return;

            switch (Action.actionType)
            {
                case ActionType.Button:
                    if (SelectedTarget.Tag is ButtonFlags buttonFlags)
                        ((ButtonActions)Action).Button = buttonFlags;
                    break;

                case ActionType.Keyboard:
                    if (SelectedTarget.Tag is VirtualKeyCode virtualKeyCode)
                        ((KeyboardActions)Action).Key = virtualKeyCode;
                    break;

                case ActionType.Joystick:
                    if (SelectedTarget.Tag is AxisLayoutFlags axisLayoutFlags)
                        ((AxisActions)Action).Axis = axisLayoutFlags;
                    break;

                case ActionType.Mouse:
                    if (SelectedTarget.Tag is MouseActionsType mouseActionsType)
                        ((MouseActions)Action).MouseType = mouseActionsType;
                    break;
            }
        }

        protected override void Update()
        {
            _parentStack.UpdateFromMapping();
        }

        protected override void Delete()
        {
            Action = null;
            _parentStack.UpdateFromMapping();
        }

        // Done from AxisStack
        protected override void UpdateMapping(Layout layout)
        {
        }
    }
}
