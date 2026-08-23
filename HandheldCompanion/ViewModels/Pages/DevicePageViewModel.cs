using HandheldCompanion.Controllers;
using HandheldCompanion.Devices;
using HandheldCompanion.Devices.Lenovo;
using HandheldCompanion.GraphicsProcessingUnit;
using HandheldCompanion.Helpers;
using HandheldCompanion.Managers;
using HandheldCompanion.Misc;
using HandheldCompanion.Models;
using HandheldCompanion.Processors;
using HandheldCompanion.ViewModels.Commands;
using HandheldCompanion.Views;
using HandheldCompanion.Watchers;
using iNKORE.UI.WPF.Modern.Controls;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using static HandheldCompanion.Devices.Lenovo.SapientiaUsb;
using static HandheldCompanion.Utils.DeviceUtils;

namespace HandheldCompanion.ViewModels
{
    public class DevicePageViewModel : BaseViewModel
    {
        #region private vars
        private IDevice CurrentDevice => IDevice.GetCurrent();

        private bool _isInternalSensorEnabled, _isExternalSensorEnabled, _isControllerSensorEnabled;
        private bool _isDynamicLightingVisible, _isLedBrightnessVisible, _isSecondColorSupported;
        private bool _isLegionGoVisible, _isLegionGoSensorSelectionVisible, _isLegionGoLeftControllerVisible, _isLegionGoRightControllerVisible;
        private bool _isMsiClawVisible, _isGamingZoneVisible, _isColorPickerEnabled = true;
        private bool _isCalibrationEnabled, _isExternalSensorExpanderEnabled, _isInternalSensorExpanderEnabled;
        private bool _isSolidColorSupported, _isBreathingSupported, _isRainbowSupported, _isWaveSupported;
        private bool _isWheelSupported, _isGradientSupported, _isAmbilightSupported, _isPresetSupported;
        private bool _hasPrebuiltShaderDownload;
        private bool _legionSettingsInitialized;
        private double _leftJoystickDeadzone, _leftAutoSleepTime;
        private double _rightJoystickDeadzone, _rightAutoSleepTime;
        private LegionTriggerDeadzone _legionTriggerDeadzoneLeft = new();
        private LegionTriggerDeadzone _legionTriggerDeadzoneRight = new();
        private List<LEDPreset> _deviceLEDPresets = [];
        private List<BatteryBypassPreset> _deviceBatteryBypassPresets = [];
        #endregion

        public bool IsUnsupportedDevice => CurrentDevice is DefaultDevice;
        public bool IsInternalSensorEnabled => _isInternalSensorEnabled;
        public bool IsExternalSensorEnabled => _isExternalSensorEnabled;
        public bool IsControllerSensorEnabled => _isControllerSensorEnabled;
        public bool IsDynamicLightingVisible => _isDynamicLightingVisible;
        public bool IsLedBrightnessVisible => _isLedBrightnessVisible;
        public bool IsSecondColorSupported => _isSecondColorSupported;
        public bool IsLegionGoVisible => _isLegionGoVisible;
        public bool IsLegionGoSensorSelectionVisible => _isLegionGoSensorSelectionVisible;
        public bool IsLegionGoLeftControllerVisible => _isLegionGoLeftControllerVisible;
        public bool IsLegionGoRightControllerVisible => _isLegionGoRightControllerVisible;
        public bool IsMsiClawVisible => _isMsiClawVisible;

        public bool IsGamingZoneVisible => _isGamingZoneVisible;
        public bool IsColorPickerEnabled => _isColorPickerEnabled;
        public bool IsCalibrationEnabled => _isCalibrationEnabled;
        public bool IsExternalSensorExpanderEnabled => _isExternalSensorExpanderEnabled;
        public bool IsInternalSensorExpanderEnabled => _isInternalSensorExpanderEnabled;

        public bool LegionControllerPassthrough
        {
            get
            {
                return ManagerFactory.settingsManager.GetBoolean("LegionControllerPassthrough");
            }
            set
            {
                if (value != LegionControllerPassthrough)
                {
                    ManagerFactory.settingsManager.SetProperty("LegionControllerPassthrough", value);
                    OnPropertyChanged(nameof(LegionControllerPassthrough));
                }
            }
        }

        private void SensorsManager_SensorSelectionChanged(SensorFamily sensorFamily) => UpdateSensorSelection(sensorFamily);

        private void SensorsManager_CalibrationModeChanged(CalibrationMode calibrationMode) => UpdateSensorSelection(SensorsManager.ActiveSensorFamily);

        public bool LegionControllerSwap
        {
            get
            {
                return ManagerFactory.settingsManager.GetBoolean("LegionControllerSwap");
            }
            set
            {
                if (value != LegionControllerSwap)
                {
                    ManagerFactory.settingsManager.SetProperty("LegionControllerSwap", value);
                    OnPropertyChanged(nameof(LegionControllerSwap));
                }
            }
        }

        public int LegionControllerGyroIndex
        {
            get
            {
                return ManagerFactory.settingsManager.GetInt("LegionControllerGyroIndex");
            }
            set
            {
                if (value != LegionControllerGyroIndex)
                {
                    ManagerFactory.settingsManager.SetProperty("LegionControllerGyroIndex", value);
                    OnPropertyChanged(nameof(LegionControllerGyroIndex));
                }
            }
        }

        public int LegionControllerMode
        {
            get
            {
                return ManagerFactory.settingsManager.GetInt("LegionControllerMode");
            }
            set
            {
                if (value != LegionControllerMode)
                {
                    ManagerFactory.settingsManager.SetProperty("LegionControllerMode", value);
                    OnPropertyChanged(nameof(LegionControllerMode));
                }
            }
        }

        public bool LegionControllerPhysicalXInput
        {
            get => ManagerFactory.settingsManager.GetBoolean("LegionControllerPhysicalXInput");
            set
            {
                if (value != LegionControllerPhysicalXInput)
                {
                    ManagerFactory.settingsManager.SetProperty("LegionControllerPhysicalXInput", value);
                    OnPropertyChanged(nameof(LegionControllerPhysicalXInput));
                }
            }
        }

        public int GamingZoneVRAM
        {
            get
            {
                return ManagerFactory.settingsManager.GetInt("ZotacGamingZoneVRAM");
            }
            set
            {
                if (value != GamingZoneVRAM)
                {
                    ManagerFactory.settingsManager.SetProperty("ZotacGamingZoneVRAM", value);
                    OnPropertyChanged(nameof(GamingZoneVRAM));
                }
            }
        }

        public bool IsSolidColorSupported => _isSolidColorSupported;
        public bool IsBreathingSupported => _isBreathingSupported;
        public bool IsRainbowSupported => _isRainbowSupported;
        public bool IsWaveSupported => _isWaveSupported;
        public bool IsWheelSupported => _isWheelSupported;
        public bool IsGradientSupported => _isGradientSupported;
        public bool IsAmbilightSupported => _isAmbilightSupported;
        public bool IsPresetSupported => _isPresetSupported;

        public double LeftJoystickDeadzone
        {
            get => _leftJoystickDeadzone;
            set
            {
                if (SetProperty(ref _leftJoystickDeadzone, value) && _legionSettingsInitialized)
                    SetStickCustomDeadzone(LegionGoTablet.LeftJoyconIndex, (int)value);
            }
        }

        public double LeftAutoSleepTime
        {
            get => _leftAutoSleepTime;
            set
            {
                if (SetProperty(ref _leftAutoSleepTime, value) && _legionSettingsInitialized)
                    SetAutoSleepTime(LegionGoTablet.LeftJoyconIndex, (int)value);
            }
        }
        public double LeftTriggerDeadzone
        {
            get => _legionTriggerDeadzoneLeft.Deadzone;
            set
            {
                if (value == LeftTriggerDeadzone)
                    return;

                _legionTriggerDeadzoneLeft.Deadzone = (int)value;
                SetTriggerDeadzoneAndMargin(LegionGoTablet.LeftJoyconIndex, _legionTriggerDeadzoneLeft);
                OnPropertyChanged(nameof(LeftTriggerDeadzone));
            }
        }

        public double LeftTriggerMargin
        {
            get => _legionTriggerDeadzoneLeft.Margin;
            set
            {
                if (value == LeftTriggerMargin)
                    return;

                _legionTriggerDeadzoneLeft.Margin = (int)value;
                SetTriggerDeadzoneAndMargin(LegionGoTablet.LeftJoyconIndex, _legionTriggerDeadzoneLeft);
                OnPropertyChanged(nameof(LeftTriggerMargin));
            }
        }
        public double RightJoystickDeadzone
        {
            get => _rightJoystickDeadzone;
            set
            {
                if (SetProperty(ref _rightJoystickDeadzone, value) && _legionSettingsInitialized)
                    SetStickCustomDeadzone(LegionGoTablet.RightJoyconIndex, (int)value);
            }
        }

        public double RightAutoSleepTime
        {
            get => _rightAutoSleepTime;
            set
            {
                if (SetProperty(ref _rightAutoSleepTime, value) && _legionSettingsInitialized)
                    SetAutoSleepTime(LegionGoTablet.RightJoyconIndex, (int)value);
            }
        }
        public double RightTriggerDeadzone
        {
            get => _legionTriggerDeadzoneRight.Deadzone;
            set
            {
                if (value == RightTriggerDeadzone)
                    return;

                _legionTriggerDeadzoneRight.Deadzone = (int)value;
                SetTriggerDeadzoneAndMargin(LegionGoTablet.RightJoyconIndex, _legionTriggerDeadzoneRight);
                OnPropertyChanged(nameof(RightTriggerDeadzone));
            }
        }

        public double RightTriggerMargin
        {
            get => _legionTriggerDeadzoneRight.Margin;
            set
            {
                if (value == RightTriggerMargin)
                    return;

                _legionTriggerDeadzoneRight.Margin = (int)value;
                SetTriggerDeadzoneAndMargin(LegionGoTablet.RightJoyconIndex, _legionTriggerDeadzoneRight);
                OnPropertyChanged(nameof(RightTriggerMargin));
            }
        }
        public List<LEDPreset> DeviceLEDPresets => _deviceLEDPresets;
        public List<BatteryBypassPreset> DeviceBatteryBypassPresets => _deviceBatteryBypassPresets;

        private void SetUiState(string propertyName, bool value, ref bool storage)
        {
            SetProperty(ref storage, value, propertyName: propertyName);
        }

        public void UpdateCapabilities(IDevice device)
        {
            SetUiState(nameof(IsInternalSensorEnabled), device.Capabilities.HasFlag(DeviceCapabilities.InternalSensor), ref _isInternalSensorEnabled);
            SetUiState(nameof(IsExternalSensorEnabled), device.Capabilities.HasFlag(DeviceCapabilities.ExternalSensor), ref _isExternalSensorEnabled);
            SetUiState(nameof(IsDynamicLightingVisible), device.Capabilities.HasFlag(DeviceCapabilities.DynamicLighting), ref _isDynamicLightingVisible);
            SetUiState(nameof(IsLedBrightnessVisible), device.Capabilities.HasFlag(DeviceCapabilities.DynamicLightingBrightness), ref _isLedBrightnessVisible);
            SetUiState(nameof(IsSecondColorSupported), device.Capabilities.HasFlag(DeviceCapabilities.DynamicLightingSecondLEDColor), ref _isSecondColorSupported);
        }

        private void OnCapabilitiesChanged(IDevice sender, DeviceCapabilities capabilities) => UpdateCapabilities(sender);

        private void ControllerManager_Initialized()
        {
            // manage events
            ControllerManager.ControllerSelected += ControllerManager_ControllerSelected;

            if (ControllerManager.HasTargetController)
                UpdateController(ControllerManager.GetTarget());
        }

        private void ControllerManager_ControllerSelected(IController controller) => UpdateController(controller);

        public void UpdateDeviceState(IDevice device)
        {
            UpdateCapabilities(device);

            SetUiState(nameof(IsLegionGoVisible), device is Devices.Lenovo.LegionGoTablet, ref _isLegionGoVisible);
            SetUiState(nameof(IsLegionGoSensorSelectionVisible), device is Devices.Lenovo.LegionGoTablet, ref _isLegionGoSensorSelectionVisible);
            SetUiState(nameof(IsLegionGoLeftControllerVisible), device is Devices.Lenovo.LegionGoTablet, ref _isLegionGoLeftControllerVisible);
            SetUiState(nameof(IsLegionGoRightControllerVisible), device is Devices.Lenovo.LegionGoTablet, ref _isLegionGoRightControllerVisible);
            SetUiState(nameof(IsMsiClawVisible), device is ClawA1M, ref _isMsiClawVisible);
            SetUiState(nameof(IsGamingZoneVisible), device is Devices.Zotac.GamingZone, ref _isGamingZoneVisible);

            SetUiState(nameof(IsSolidColorSupported), device.DynamicLightingCapabilities.HasFlag(LEDLevel.SolidColor), ref _isSolidColorSupported);
            SetUiState(nameof(IsBreathingSupported), device.DynamicLightingCapabilities.HasFlag(LEDLevel.Breathing), ref _isBreathingSupported);
            SetUiState(nameof(IsRainbowSupported), device.DynamicLightingCapabilities.HasFlag(LEDLevel.Rainbow), ref _isRainbowSupported);
            SetUiState(nameof(IsWaveSupported), device.DynamicLightingCapabilities.HasFlag(LEDLevel.Wave), ref _isWaveSupported);
            SetUiState(nameof(IsWheelSupported), device.DynamicLightingCapabilities.HasFlag(LEDLevel.Wheel), ref _isWheelSupported);
            SetUiState(nameof(IsGradientSupported), device.DynamicLightingCapabilities.HasFlag(LEDLevel.Gradient), ref _isGradientSupported);
            SetUiState(nameof(IsAmbilightSupported), device.DynamicLightingCapabilities.HasFlag(LEDLevel.Ambilight), ref _isAmbilightSupported);
            SetUiState(nameof(IsPresetSupported), device.DynamicLightingCapabilities.HasFlag(LEDLevel.LEDPreset), ref _isPresetSupported);
        }

        public void UpdateController(IController? controller)
        {
            SetUiState(nameof(IsControllerSensorEnabled), controller?.Capabilities.HasFlag(ControllerCapabilities.MotionSensor) == true, ref _isControllerSensorEnabled);
        }

        public void UpdateAccentColorState(bool useAccentColor)
        {
            SetUiState(nameof(IsColorPickerEnabled), !useAccentColor, ref _isColorPickerEnabled);
        }

        public void UpdateSensorSelection(SensorFamily sensorFamily)
        {
            SensorFamily activeSensorFamily = sensorFamily == SensorFamily.Auto ? SensorsManager.ActiveSensorFamily : sensorFamily;

            bool isExternal = activeSensorFamily == SensorFamily.SerialUSBIMU;
            bool isInternal = activeSensorFamily == SensorFamily.Windows;
            bool isController = activeSensorFamily == SensorFamily.Controller;

            SetUiState(nameof(IsCalibrationEnabled), SensorsManager.ActiveCalibrationMode == CalibrationMode.Manual && (isExternal || isInternal || isController), ref _isCalibrationEnabled);
            SetUiState(nameof(IsExternalSensorExpanderEnabled), isExternal, ref _isExternalSensorExpanderEnabled);
            SetUiState(nameof(IsInternalSensorExpanderEnabled), isInternal, ref _isInternalSensorExpanderEnabled);
        }

        private void Device_Opened(IDevice sender)
        {
            // manage events
            CurrentDevice.CapabilitiesChanged += OnCapabilitiesChanged;

            UpdateDeviceState(sender);
            RefreshIMUMatrix();

            if (sender is LegionGoTablet)
            {
                _legionTriggerDeadzoneLeft = GetTriggerDeadzoneAndMargin(LegionGoTablet.LeftJoyconIndex);
                _legionTriggerDeadzoneRight = GetTriggerDeadzoneAndMargin(LegionGoTablet.RightJoyconIndex);

                SetProperty(ref _leftJoystickDeadzone, GetStickCustomDeadzone(LegionGoTablet.LeftJoyconIndex), propertyName: nameof(LeftJoystickDeadzone));
                SetProperty(ref _leftAutoSleepTime, GetAutoSleepTime(LegionGoTablet.LeftJoyconIndex), propertyName: nameof(LeftAutoSleepTime));
                OnPropertyChanged(nameof(LeftTriggerDeadzone));
                OnPropertyChanged(nameof(LeftTriggerMargin));
                SetProperty(ref _rightJoystickDeadzone, GetStickCustomDeadzone(LegionGoTablet.RightJoyconIndex), propertyName: nameof(RightJoystickDeadzone));
                SetProperty(ref _rightAutoSleepTime, GetAutoSleepTime(LegionGoTablet.RightJoyconIndex), propertyName: nameof(RightAutoSleepTime));
                OnPropertyChanged(nameof(RightTriggerDeadzone));
                OnPropertyChanged(nameof(RightTriggerMargin));
                _legionSettingsInitialized = true;
            }

            if (sender is OneXPlayerX1)
            {
                SetProperty(ref _deviceLEDPresets, sender.LEDPresets, propertyName: nameof(DeviceLEDPresets));
                SetProperty(ref _deviceBatteryBypassPresets, sender.BatteryBypassPresets, propertyName: nameof(DeviceBatteryBypassPresets));
            }
        }

        private void Device_Closed(IDevice sender)
        {
        }

        #region Battery bypass
        public int BatteryBypassMin => CurrentDevice.BatteryBypassMin;
        public int BatteryBypassMax => CurrentDevice.BatteryBypassMax;
        public int BatteryBypassStep => CurrentDevice.BatteryBypassStep;
        public Visibility BatteryBypassVisibility => BatteryChargeLimitCapacity ? Visibility.Visible : Visibility.Collapsed;
        public Visibility BatteryBypassModeVisibility => CurrentDevice.Capabilities.HasFlag(DeviceCapabilities.BatteryBypassCharging) ? Visibility.Visible : Visibility.Collapsed;
        public bool BatteryChargeLimitCapacity => CurrentDevice.Capabilities.HasFlag(DeviceCapabilities.BatteryChargeLimit);

        public bool BatteryChargeLimit
        {
            get
            {
                return ManagerFactory.settingsManager.GetBoolean("BatteryChargeLimit");
            }
            set
            {
                if (value != BatteryChargeLimit)
                {
                    ManagerFactory.settingsManager.SetProperty("BatteryChargeLimit", value);
                    OnPropertyChanged(nameof(BatteryChargeLimit));
                }
            }
        }

        public double BatteryChargeLimitPercent
        {
            get
            {
                return ManagerFactory.settingsManager.GetDouble("BatteryChargeLimitPercent");
            }
            set
            {
                if (value != BatteryChargeLimitPercent)
                {
                    ManagerFactory.settingsManager.SetProperty("BatteryChargeLimitPercent", value);
                    OnPropertyChanged(nameof(BatteryChargeLimitPercent));
                }
            }
        }
        #endregion

        #region Power options
        public bool HasWMIMethod => CurrentDevice.Capabilities.HasFlag(DeviceCapabilities.OEMCPU);

        public bool HasPrebuiltShaderDownload => _hasPrebuiltShaderDownload;

        public bool PrebuiltShaderDownloadEnabled
        {
            get => GPUManager.GetCurrent() is GPU gpu && gpu.GetPrebuiltShaderDownload(null, out bool enabled) && enabled;
            set
            {
                if (GPUManager.GetCurrent() is GPU gpu && gpu.SetPrebuiltShaderDownload(null, value))
                    OnPropertyChanged(nameof(PrebuiltShaderDownloadEnabled));
            }
        }

        public bool GoBackToSleep
        {
            get
            {
                return ManagerFactory.settingsManager.GetBoolean("GoBackToSleep");
            }
            set
            {
                if (value != GoBackToSleep)
                {
                    ManagerFactory.settingsManager.SetProperty("GoBackToSleep", value);
                    OnPropertyChanged(nameof(GoBackToSleep));
                }
            }
        }
        public int ConfigurableTDPMethod
        {
            get
            {
                return ManagerFactory.settingsManager.GetInt("ConfigurableTDPMethod");
            }
            set
            {
                if (value != ConfigurableTDPMethod)
                {
                    ManagerFactory.settingsManager.SetProperty("ConfigurableTDPMethod", value);
                    OnPropertyChanged(nameof(ConfigurableTDPMethod));
                }
            }
        }
        #endregion

        #region MSIClaw
        public int ClawControllerIndex
        {
            get
            {
                return ManagerFactory.settingsManager.GetInt("MSIClawControllerIndex");
            }
            set
            {
                if (value != ClawControllerIndex)
                {
                    ManagerFactory.settingsManager.SetProperty("MSIClawControllerIndex", value);
                    OnPropertyChanged(nameof(ClawControllerIndex));
                }
            }
        }

        public bool BlockMsiClawWinGHotkey
        {
            get
            {
                return ManagerFactory.settingsManager.GetBoolean("BlockMsiClawWinGHotkey");
            }
            set
            {
                if (value != BlockMsiClawWinGHotkey)
                {
                    ManagerFactory.settingsManager.SetProperty("BlockMsiClawWinGHotkey", value);
                    OnPropertyChanged(nameof(BlockMsiClawWinGHotkey));
                }
            }
        }
        #endregion

        #region Overlay
        public bool IsOverlayGamepadVisible => App.overlayModel?.Visibility == System.Windows.Visibility.Visible;

        private ICommand? _toggleOverlayGamepadCommand;
        public ICommand ToggleOverlayGamepadCommand => _toggleOverlayGamepadCommand ??= new RelayCommand(_ => OnToggleOverlayGamepad());

        private void OnToggleOverlayGamepad()
        {
            App.overlayModel?.ToggleVisibility();
            OnPropertyChanged(nameof(IsOverlayGamepadVisible));
        }
        #endregion

        #region IMU Configuration
        private float _GyroAxisX;
        public float GyroAxisX
        {
            get => _GyroAxisX;
            set
            {
                if (value != _GyroAxisX)
                {
                    _GyroAxisX = value;
                    CurrentDevice.GyroMatrix.Axis.X = value;

                    CurrentDevice.UpdateIMUMatrix(IMUMatrixType.Gyro, CurrentDevice.GyroMatrix);
                    OnPropertyChanged(nameof(GyroAxisX));
                }
            }
        }

        private float _GyroAxisY;
        public float GyroAxisY
        {
            get => _GyroAxisY;
            set
            {
                if (value != _GyroAxisY)
                {
                    _GyroAxisY = value;
                    CurrentDevice.GyroMatrix.Axis.Y = value;

                    CurrentDevice.UpdateIMUMatrix(IMUMatrixType.Gyro, CurrentDevice.GyroMatrix);
                    OnPropertyChanged(nameof(GyroAxisY));
                }
            }
        }

        private float _GyroAxisZ;
        public float GyroAxisZ
        {
            get => _GyroAxisZ;
            set
            {
                if (value != _GyroAxisZ)
                {
                    _GyroAxisZ = value;
                    CurrentDevice.GyroMatrix.Axis.Z = value;

                    CurrentDevice.UpdateIMUMatrix(IMUMatrixType.Gyro, CurrentDevice.GyroMatrix);
                    OnPropertyChanged(nameof(GyroAxisZ));
                }
            }
        }

        private float _AcceleroAxisX;
        public float AcceleroAxisX
        {
            get => _AcceleroAxisX;
            set
            {
                if (value != _AcceleroAxisX)
                {
                    _AcceleroAxisX = value;
                    CurrentDevice.AcceleroMatrix.Axis.X = value;

                    CurrentDevice.UpdateIMUMatrix(IMUMatrixType.Accelero, CurrentDevice.AcceleroMatrix);
                    OnPropertyChanged(nameof(AcceleroAxisX));
                }
            }
        }

        private float _AcceleroAxisY;
        public float AcceleroAxisY
        {
            get => _AcceleroAxisY;
            set
            {
                if (value != _AcceleroAxisY)
                {
                    _AcceleroAxisY = value;
                    CurrentDevice.AcceleroMatrix.Axis.Y = value;

                    CurrentDevice.UpdateIMUMatrix(IMUMatrixType.Accelero, CurrentDevice.AcceleroMatrix);
                    OnPropertyChanged(nameof(AcceleroAxisY));
                }
            }
        }

        private float _AcceleroAxisZ;
        public float AcceleroAxisZ
        {
            get => _AcceleroAxisZ;
            set
            {
                if (value != _AcceleroAxisZ)
                {
                    _AcceleroAxisZ = value;
                    CurrentDevice.AcceleroMatrix.Axis.Z = value;

                    CurrentDevice.UpdateIMUMatrix(IMUMatrixType.Accelero, CurrentDevice.AcceleroMatrix);
                    OnPropertyChanged(nameof(AcceleroAxisZ));
                }
            }
        }

        #region AxisSwap Configuration
        /// <summary>
        /// Swaps axis mappings to maintain one-to-one mapping when changing an output axis.
        /// If setting output X to Y, find what's currently mapped to X and swap it to the old Y value.
        /// </summary>
        private void SwapAxisMappingIfNeeded(IMUMatrix matrix, char outputAxis, char newInputAxis)
        {
            char oldInputAxis = matrix.AxisSwap[outputAxis];

            // If not actually changing, do nothing
            if (oldInputAxis == newInputAxis)
                return;

            // Find which output axis currently maps to the new input axis
            char conflictingOutputAxis = '\0';
            foreach (var kvp in matrix.AxisSwap)
            {
                if (kvp.Value == newInputAxis && kvp.Key != outputAxis)
                {
                    conflictingOutputAxis = kvp.Key;
                    break;
                }
            }

            // If another output is using the new input axis, swap it with the old input
            if (conflictingOutputAxis != '\0')
            {
                matrix.AxisSwap[conflictingOutputAxis] = oldInputAxis;
            }
        }

        public char GyroAxisSwapX
        {
            get => CurrentDevice.GyroMatrix.AxisSwap['X'];
            set
            {
                if (value != GyroAxisSwapX)
                {
                    SwapAxisMappingIfNeeded(CurrentDevice.GyroMatrix, 'X', value);
                    CurrentDevice.GyroMatrix.AxisSwap['X'] = value;

                    CurrentDevice.UpdateIMUMatrix(IMUMatrixType.Gyro, CurrentDevice.GyroMatrix);
                    OnPropertyChanged(nameof(GyroAxisSwapX));
                    OnPropertyChanged(nameof(GyroAxisSwapY));
                    OnPropertyChanged(nameof(GyroAxisSwapZ));
                }
            }
        }

        public char GyroAxisSwapY
        {
            get => CurrentDevice.GyroMatrix.AxisSwap['Y'];
            set
            {
                if (value != GyroAxisSwapY)
                {
                    SwapAxisMappingIfNeeded(CurrentDevice.GyroMatrix, 'Y', value);
                    CurrentDevice.GyroMatrix.AxisSwap['Y'] = value;

                    CurrentDevice.UpdateIMUMatrix(IMUMatrixType.Gyro, CurrentDevice.GyroMatrix);
                    OnPropertyChanged(nameof(GyroAxisSwapX));
                    OnPropertyChanged(nameof(GyroAxisSwapY));
                    OnPropertyChanged(nameof(GyroAxisSwapZ));
                }
            }
        }

        public char GyroAxisSwapZ
        {
            get => CurrentDevice.GyroMatrix.AxisSwap['Z'];
            set
            {
                if (value != GyroAxisSwapZ)
                {
                    SwapAxisMappingIfNeeded(CurrentDevice.GyroMatrix, 'Z', value);
                    CurrentDevice.GyroMatrix.AxisSwap['Z'] = value;

                    CurrentDevice.UpdateIMUMatrix(IMUMatrixType.Gyro, CurrentDevice.GyroMatrix);
                    OnPropertyChanged(nameof(GyroAxisSwapX));
                    OnPropertyChanged(nameof(GyroAxisSwapY));
                    OnPropertyChanged(nameof(GyroAxisSwapZ));
                }
            }
        }

        public char AcceleroAxisSwapX
        {
            get => CurrentDevice.AcceleroMatrix.AxisSwap['X'];
            set
            {
                if (value != AcceleroAxisSwapX)
                {
                    SwapAxisMappingIfNeeded(CurrentDevice.AcceleroMatrix, 'X', value);
                    CurrentDevice.AcceleroMatrix.AxisSwap['X'] = value;

                    CurrentDevice.UpdateIMUMatrix(IMUMatrixType.Accelero, CurrentDevice.AcceleroMatrix);
                    OnPropertyChanged(nameof(AcceleroAxisSwapX));
                    OnPropertyChanged(nameof(AcceleroAxisSwapY));
                    OnPropertyChanged(nameof(AcceleroAxisSwapZ));
                }
            }
        }

        public char AcceleroAxisSwapY
        {
            get => CurrentDevice.AcceleroMatrix.AxisSwap['Y'];
            set
            {
                if (value != AcceleroAxisSwapY)
                {
                    SwapAxisMappingIfNeeded(CurrentDevice.AcceleroMatrix, 'Y', value);
                    CurrentDevice.AcceleroMatrix.AxisSwap['Y'] = value;

                    CurrentDevice.UpdateIMUMatrix(IMUMatrixType.Accelero, CurrentDevice.AcceleroMatrix);
                    OnPropertyChanged(nameof(AcceleroAxisSwapX));
                    OnPropertyChanged(nameof(AcceleroAxisSwapY));
                    OnPropertyChanged(nameof(AcceleroAxisSwapZ));
                }
            }
        }

        public char AcceleroAxisSwapZ
        {
            get => CurrentDevice.AcceleroMatrix.AxisSwap['Z'];
            set
            {
                if (value != AcceleroAxisSwapZ)
                {
                    SwapAxisMappingIfNeeded(CurrentDevice.AcceleroMatrix, 'Z', value);
                    CurrentDevice.AcceleroMatrix.AxisSwap['Z'] = value;

                    CurrentDevice.UpdateIMUMatrix(IMUMatrixType.Accelero, CurrentDevice.AcceleroMatrix);
                    OnPropertyChanged(nameof(AcceleroAxisSwapX));
                    OnPropertyChanged(nameof(AcceleroAxisSwapY));
                    OnPropertyChanged(nameof(AcceleroAxisSwapZ));
                }
            }
        }
        #endregion

        public void RefreshIMUMatrix()
        {
            _GyroAxisX = CurrentDevice.GyroMatrix.Axis.X;
            _GyroAxisY = CurrentDevice.GyroMatrix.Axis.Y;
            _GyroAxisZ = CurrentDevice.GyroMatrix.Axis.Z;
            _AcceleroAxisX = CurrentDevice.AcceleroMatrix.Axis.X;
            _AcceleroAxisY = CurrentDevice.AcceleroMatrix.Axis.Y;
            _AcceleroAxisZ = CurrentDevice.AcceleroMatrix.Axis.Z;

            OnPropertyChanged(nameof(GyroAxisX));
            OnPropertyChanged(nameof(GyroAxisY));
            OnPropertyChanged(nameof(GyroAxisZ));
            OnPropertyChanged(nameof(AcceleroAxisX));
            OnPropertyChanged(nameof(AcceleroAxisY));
            OnPropertyChanged(nameof(AcceleroAxisZ));

            OnPropertyChanged(nameof(GyroAxisSwapX));
            OnPropertyChanged(nameof(GyroAxisSwapY));
            OnPropertyChanged(nameof(GyroAxisSwapZ));
            OnPropertyChanged(nameof(AcceleroAxisSwapX));
            OnPropertyChanged(nameof(AcceleroAxisSwapY));
            OnPropertyChanged(nameof(AcceleroAxisSwapZ));
        }
        #endregion

        #region MemoryIntegrity
        private CoreIsolationWatcher coreIsolationWatcher = new CoreIsolationWatcher();
        public bool MemoryIntegrity
        {
            get
            {
                return coreIsolationWatcher.VulnerableDriverBlocklistEnable || coreIsolationWatcher.HypervisorEnforcedCodeIntegrityEnabled || coreIsolationWatcher.SmartAppControlEnabled;
            }
            set
            {
                coreIsolationWatcher.SetSettings(value, MainWindow.GetCurrent());
            }
        }
        #endregion

        #region Manufacturer application
        private ISpaceWatcher? manufacturerWatcher;

        private bool _ManufacturerAppBusy;
        public bool ManufacturerAppBusy
        {
            get
            {
                return !_ManufacturerAppBusy;
            }
            set
            {
                if (value != _ManufacturerAppBusy)
                {
                    _ManufacturerAppBusy = value;
                    OnPropertyChanged(nameof(ManufacturerAppBusy));
                }
            }
        }

        public bool ManufacturerAppStatus
        {
            get
            {
                return manufacturerWatcher?.IsRunning ?? false;
            }
            set
            {
                // update flag
                ManufacturerAppBusy = true;

                _ = Task.Run(async () =>
                {
                    // Enable or disable the manufacturer software
                    if (value)
                        manufacturerWatcher?.Enable();
                    else
                        manufacturerWatcher?.Disable();
                });
            }
        }

        public bool HasManufacturerPlatform => manufacturerWatcher is not null;
        #endregion

        #region AdvancedSettings
        public bool IsIntel => PerformanceManager.GetProcessor() is IntelProcessor;
        public bool IsAMD => PerformanceManager.GetProcessor() is AMDProcessor;

        public bool CanSetCoreCurve => PerformanceManager.GetProcessor() is AMDProcessor AMD && AMD.HasAllCoreCurve && HasAdvancedSettings;
        public bool CanSetGPUCurve => PerformanceManager.GetProcessor() is AMDProcessor AMD && AMD.CanSetGPUCurve && HasAdvancedSettings;
        public bool CanSetTemperature => PerformanceManager.GetProcessor() is AMDProcessor AMD && (AMD.CanSetTctl || AMD.CanSetChtc || AMD.CanSetSkinTemperature) && HasAdvancedSettings;
        public bool CanSetTctlTemperature => PerformanceManager.GetProcessor() is AMDProcessor AMD && (AMD.CanSetTctl || AMD.CanSetChtc) && HasAdvancedSettings;
        public bool CanSetSkinTemperature => PerformanceManager.GetProcessor() is AMDProcessor AMD && AMD.CanSetSkinTemperature && HasAdvancedSettings;

        public uint TctlLimit
        {
            get
            {
                // If value is 0 (not yet initialized), load from hardware
                uint storedValue = (uint)ManagerFactory.settingsManager.GetInt("TctlLimit");
                if (storedValue == 0)
                    storedValue = CurrentDevice.Tjmax;

                return storedValue;
            }
            set
            {
                if (value != TctlLimit && CurrentDevice.ApplyTctlLimit(value))
                {
                    ManagerFactory.settingsManager.SetProperty("TctlLimit", (int)value);
                    OnPropertyChanged(nameof(TctlLimit));
                }
            }
        }

        public uint SkinTemperatureLimit
        {
            get
            {
                // If value is 0 (not yet initialized), load from hardware
                uint storedValue = (uint)ManagerFactory.settingsManager.GetInt("SkinTemperatureLimit");
                if (storedValue == 0)
                    storedValue = CurrentDevice.Tskin;

                return storedValue;
            }
            set
            {
                if (value != SkinTemperatureLimit && CurrentDevice.ApplySkinTemperatureLimit(value))
                {
                    ManagerFactory.settingsManager.SetProperty("SkinTemperatureLimit", (int)value);
                    OnPropertyChanged(nameof(SkinTemperatureLimit));
                }
            }
        }

        public bool HasAdvancedSettings
        {
            get
            {
                return ManagerFactory.settingsManager.GetBoolean("ConfigurableTDPOverride");
            }
            set
            {
                if (value != HasAdvancedSettings)
                    _ = SetHasAdvancedSettingsAsync(value);
            }
        }

        private async Task SetHasAdvancedSettingsAsync(bool value)
        {
            if (value)
            {
                ContentDialogResult result = await new Dialog(MainWindow.GetCurrent())
                {
                    Title = "Warning",
                    Content = "Altering CPU power or voltage values might cause instabilities. Product warranties may not apply if the processor is operated beyond its specifications. Use at your own risk.",
                    CloseButtonText = Properties.Resources.ProfilesPage_Cancel,
                    PrimaryButtonText = Properties.Resources.ProfilesPage_OK
                }.ShowAsync();

                if (result != ContentDialogResult.Primary)
                    return;
            }

            ManagerFactory.settingsManager.SetProperty("ConfigurableTDPOverride", value);
            OnPropertyChanged(nameof(HasAdvancedSettings));
            OnPropertyChanged(nameof(CanSetCoreCurve));
            OnPropertyChanged(nameof(CanSetGPUCurve));
            OnPropertyChanged(nameof(CanSetTemperature));
            OnPropertyChanged(nameof(CanSetTctlTemperature));
            OnPropertyChanged(nameof(CanSetSkinTemperature));
        }
        #endregion

        public DevicePageViewModel()
        {
            // manufacturer watcher
            manufacturerWatcher = ISpaceWatcher.CreateCurrent();

            if (manufacturerWatcher is not null)
            {
                // start watcher
                manufacturerWatcher.StatusChanged += ManufacturerWatcher_StatusChanged;
                manufacturerWatcher.Start();
            }

            // raise events
            switch (ManagerFactory.settingsManager.Status)
            {
                default:
                case ManagerStatus.Initializing:
                    ManagerFactory.settingsManager.Initialized += SettingsManager_Initialized;
                    break;
                case ManagerStatus.Initialized:
                    QuerySettings();
                    break;
            }

            // raise events
            switch (ManagerFactory.gpuManager.Status)
            {
                default:
                case ManagerStatus.Initializing:
                    ManagerFactory.gpuManager.Initialized += GpuManager_Initialized;
                    break;
                case ManagerStatus.Initialized:
                    QueryGPU();
                    break;
            }

            // manage events
            CurrentDevice.Opened += Device_Opened;
            CurrentDevice.Closed += Device_Closed;
            if (CurrentDevice.IsOpen)
                Device_Opened(CurrentDevice);

            // manage events
            SensorsManager.Initialized += SensorsManager_Initialized;
            if (SensorsManager.IsInitialized)
                UpdateSensorSelection(SensorsManager.ActiveSensorFamily);

            // manage events
            ControllerManager.Initialized += ControllerManager_Initialized;
            if (ControllerManager.IsInitialized)
                ControllerManager_Initialized();

            // manage events
            PerformanceManager.Initialized += PerformanceManager_Initialized;
            if (PerformanceManager.IsInitialized && PerformanceManager.GetProcessor() is Processor processor)
                PerformanceManager_Initialized(processor.CanChangeTDP, processor.CanChangeGPU);
        }

        private void SensorsManager_Initialized()
        {
            // manage events
            SensorsManager.SensorSelectionChanged += SensorsManager_SensorSelectionChanged;
            SensorsManager.CalibrationModeChanged += SensorsManager_CalibrationModeChanged;

            // raise events
            SensorsManager_SensorSelectionChanged(SensorsManager.ActiveSensorFamily);
        }

        private void PerformanceManager_Initialized(bool canChangeTDP, bool canChangeGPU)
        {
            QueryProcessor();
        }

        private void GpuManager_Initialized()
        {
            QueryGPU();
        }

        private void QueryGPU()
        {
            ManagerFactory.gpuManager.Hooked += GPUManager_Hooked;
            ManagerFactory.gpuManager.Unhooked += GPUManager_Unhooked;

            if (GPUManager.GetCurrent() is GPU gpu)
                GPUManager_Hooked(gpu);
        }

        private void GPUManager_Hooked(GPU gpu)
        {
            _hasPrebuiltShaderDownload = gpu.HasPrebuiltShaderDownload(out _);
            OnPropertyChanged(nameof(HasPrebuiltShaderDownload));
            OnPropertyChanged(nameof(PrebuiltShaderDownloadEnabled));
        }

        private void GPUManager_Unhooked(GPU gpu)
        {
            _hasPrebuiltShaderDownload = false;
            OnPropertyChanged(nameof(HasPrebuiltShaderDownload));
            OnPropertyChanged(nameof(PrebuiltShaderDownloadEnabled));
        }

        private void QueryProcessor()
        {
            if (PerformanceManager.GetProcessor() is IntelProcessor)
            {
                coreIsolationWatcher.StatusChanged += CoreIsolationWatcher_StatusChanged;
                coreIsolationWatcher.Start();
            }

            OnPropertyChanged(nameof(IsIntel));
            OnPropertyChanged(nameof(IsAMD));
            OnPropertyChanged(nameof(CanSetCoreCurve));
            OnPropertyChanged(nameof(CanSetGPUCurve));
        }

        private void SettingsManager_Initialized()
        {
            QuerySettings();
        }

        private void QuerySettings()
        {
            // manage events
            ManagerFactory.settingsManager.SettingValueChanged += SettingsManager_SettingValueChanged;

            // raise events
            SettingsManager_SettingValueChanged("BatteryChargeLimit", ManagerFactory.settingsManager.GetBoolean("BatteryChargeLimit"), false, true);
            SettingsManager_SettingValueChanged("BatteryChargeLimitPercent", ManagerFactory.settingsManager.GetDouble("BatteryChargeLimitPercent"), false, true);
            SettingsManager_SettingValueChanged("GoBackToSleep", ManagerFactory.settingsManager.GetBoolean("GoBackToSleep"), false, true);
            SettingsManager_SettingValueChanged("LEDSettingsUseAccentColor", ManagerFactory.settingsManager.GetBoolean("LEDSettingsUseAccentColor"), false, true);
            SettingsManager_SettingValueChanged("LegionControllerPassthrough", ManagerFactory.settingsManager.GetBoolean("LegionControllerPassthrough"), false, true);
            SettingsManager_SettingValueChanged("LegionControllerPhysicalXInput", ManagerFactory.settingsManager.GetBoolean("LegionControllerPhysicalXInput"), false, true);
            SettingsManager_SettingValueChanged("LegionControllerSwap", ManagerFactory.settingsManager.GetBoolean("LegionControllerSwap"), false, true);
            SettingsManager_SettingValueChanged("LegionControllerGyroIndex", ManagerFactory.settingsManager.GetInt("LegionControllerGyroIndex"), false, true);
            SettingsManager_SettingValueChanged("LegionControllerMode", ManagerFactory.settingsManager.GetInt("LegionControllerMode"), false, true);
            SettingsManager_SettingValueChanged("ZotacGamingZoneVRAM", ManagerFactory.settingsManager.GetInt("ZotacGamingZoneVRAM"), false, true);
            SettingsManager_SettingValueChanged("BlockMsiClawWinGHotkey", ManagerFactory.settingsManager.GetBoolean("BlockMsiClawWinGHotkey"), false, true);
        }

        private void CoreIsolationWatcher_StatusChanged(bool enabled)
        {
            var notification = coreIsolationWatcher.notification;
            if (notification is null)
                return;

            switch (enabled)
            {
                case true:
                    ManagerFactory.notificationManager.Add(notification);
                    break;
                case false:
                    ManagerFactory.notificationManager.Discard(notification);
                    break;
            }

            OnPropertyChanged(nameof(MemoryIntegrity));
        }

        private void ManufacturerWatcher_StatusChanged(bool enabled)
        {
            var notification = manufacturerWatcher?.notification;
            if (notification is null)
                return;

            switch (enabled)
            {
                case true:
                    ManagerFactory.notificationManager.Add(notification);
                    break;
                case false:
                    ManagerFactory.notificationManager.Discard(notification);
                    break;
            }

            // update flag
            ManufacturerAppBusy = false;
            OnPropertyChanged(nameof(ManufacturerAppStatus));
        }

        private void SettingsManager_SettingValueChanged(string name, object? value, bool temporary, bool initializing)
        {
            switch (name)
            {
                case "BatteryChargeLimit":
                    BatteryChargeLimit = Convert.ToBoolean(value);
                    break;
                case "BatteryChargeLimitPercent":
                    BatteryChargeLimitPercent = Convert.ToDouble(value);
                    break;
                case "GoBackToSleep":
                    GoBackToSleep = Convert.ToBoolean(value);
                    break;
                case "LEDSettingsUseAccentColor":
                    UpdateAccentColorState(Convert.ToBoolean(value));
                    break;
                case "LegionControllerPassthrough":
                    OnPropertyChanged(nameof(LegionControllerPassthrough));
                    break;
                case "LegionControllerPhysicalXInput":
                    OnPropertyChanged(nameof(LegionControllerPhysicalXInput));
                    break;
                case "LegionControllerSwap":
                    OnPropertyChanged(nameof(LegionControllerSwap));
                    break;
                case "LegionControllerGyroIndex":
                    OnPropertyChanged(nameof(LegionControllerGyroIndex));
                    break;
                case "LegionControllerMode":
                    OnPropertyChanged(nameof(LegionControllerMode));
                    break;
                case "ZotacGamingZoneVRAM":
                    OnPropertyChanged(nameof(GamingZoneVRAM));
                    break;
                case "BlockMsiClawWinGHotkey":
                    OnPropertyChanged(nameof(BlockMsiClawWinGHotkey));
                    break;
            }
        }

        public override void Dispose()
        {
            base.Dispose();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                CurrentDevice.CapabilitiesChanged -= OnCapabilitiesChanged;
                CurrentDevice.Opened -= Device_Opened;
                CurrentDevice.Closed -= Device_Closed;
                SensorsManager.Initialized -= SensorsManager_Initialized;
                SensorsManager.SensorSelectionChanged -= SensorsManager_SensorSelectionChanged;
                SensorsManager.CalibrationModeChanged -= SensorsManager_CalibrationModeChanged;
                ControllerManager.Initialized -= ControllerManager_Initialized;
                ControllerManager.ControllerSelected -= ControllerManager_ControllerSelected;
                PerformanceManager.Initialized -= PerformanceManager_Initialized;
                coreIsolationWatcher.StatusChanged -= CoreIsolationWatcher_StatusChanged;
                coreIsolationWatcher.Stop();
                coreIsolationWatcher.Dispose();

                if (manufacturerWatcher is not null)
                {
                    manufacturerWatcher.StatusChanged -= ManufacturerWatcher_StatusChanged;
                    manufacturerWatcher.Stop();
                    manufacturerWatcher.Dispose();
                }

                // manage events
                ManagerFactory.settingsManager.Initialized -= SettingsManager_Initialized;
                ManagerFactory.settingsManager.SettingValueChanged -= SettingsManager_SettingValueChanged;
                ManagerFactory.gpuManager.Initialized -= GpuManager_Initialized;
                ManagerFactory.gpuManager.Hooked -= GPUManager_Hooked;
                ManagerFactory.gpuManager.Unhooked -= GPUManager_Unhooked;
            }

            base.Dispose(disposing);
        }
    }
}