using HandheldCompanion.Controllers;
using HandheldCompanion.Devices;
using HandheldCompanion.Helpers;
using HandheldCompanion.Misc;
using HandheldCompanion.Sensors;
using HandheldCompanion.Shared;
using HandheldCompanion.Views;
using Nefarius.Utilities.DeviceManagement.PnP;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;
using static HandheldCompanion.Utils.DeviceUtils;

namespace HandheldCompanion.Managers
{
    public static class SensorsManager
    {
        private static IMUGyrometer? Gyrometer;
        private static IMUAccelerometer? Accelerometer;
        private static SerialUSBIMU? USBSensor;

        private static SensorFamily sensorFamily;
        private static SensorFamily sensorSelection;
        private static CalibrationMode calibrationMode = CalibrationMode.Manual;

        public static SensorFamily ActiveSensorFamily => sensorFamily;
        public static CalibrationMode ActiveCalibrationMode => calibrationMode;

        public static bool IsInitialized;

        public static event InitializedEventHandler? Initialized;
        public delegate void InitializedEventHandler();

        public static event Action<SensorFamily>? SensorSelectionChanged;
        public static event Action<CalibrationMode>? CalibrationModeChanged;

        public static void Start()
        {
            if (IsInitialized)
                return;

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
            switch (ManagerFactory.deviceManager.Status)
            {
                default:
                case ManagerStatus.Initializing:
                    ManagerFactory.deviceManager.Initialized += DeviceManager_Initialized;
                    break;
                case ManagerStatus.Initialized:
                    QueryDevice();
                    break;
            }

            // manage events
            ControllerManager.Initialized += ControllerManager_Initialized;

            // raise events
            if (ControllerManager.IsInitialized)
                ControllerManager_Initialized();

            IsInitialized = true;
            Initialized?.Invoke();

            LogManager.LogInformation("{0} has started", "SensorsManager");
        }

        private static void ControllerManager_Initialized()
        {
            // manage events
            ControllerManager.ControllerSelected += ControllerManager_ControllerSelected;
            ControllerManager.ControllerUnplugged += ControllerManager_ControllerUnplugged;

            // raise events
            if (ControllerManager.HasTargetController && ControllerManager.GetTarget() is IController controller)
                ControllerManager_ControllerSelected(controller);
        }

        private static void QueryDevice()
        {
            // manage events
            ManagerFactory.deviceManager.UsbDeviceArrived += DeviceManager_UsbDeviceArrived;
            ManagerFactory.deviceManager.UsbDeviceRemoved += DeviceManager_UsbDeviceRemoved;

            // raise events
            DeviceManager_UsbDeviceArrived(null, Guid.Empty);
        }

        private static void DeviceManager_Initialized()
        {
            QueryDevice();
        }

        private static void SettingsManager_Initialized()
        {
            QuerySettings();
        }

        private static void QuerySettings()
        {
            // manage events
            ManagerFactory.settingsManager.SettingValueChanged += SettingsManager_SettingValueChanged;

            // raise events
            SettingsManager_SettingValueChanged("SensorPlacement", ManagerFactory.settingsManager.GetString("SensorPlacement"), false, true);
            SettingsManager_SettingValueChanged("SensorPlacementUpsideDown", ManagerFactory.settingsManager.GetString("SensorPlacementUpsideDown"), false, true);
            SettingsManager_SettingValueChanged("SensorSelection", ManagerFactory.settingsManager.GetString("SensorSelection"), false, true);
            SettingsManager_SettingValueChanged("SensorCalibrationMode", ManagerFactory.settingsManager.GetString("SensorCalibrationMode"), false, true);
        }

        public static void Stop()
        {
            if (!IsInitialized)
                return;

            // stop underlying sensor reads
            StopListening();
            USBSensor?.Close();

            // manage events
            ManagerFactory.deviceManager.Initialized -= DeviceManager_Initialized;
            ManagerFactory.deviceManager.UsbDeviceArrived -= DeviceManager_UsbDeviceArrived;
            ManagerFactory.deviceManager.UsbDeviceRemoved -= DeviceManager_UsbDeviceRemoved;

            ControllerManager.Initialized -= ControllerManager_Initialized;
            ControllerManager.ControllerSelected -= ControllerManager_ControllerSelected;
            ControllerManager.ControllerUnplugged -= ControllerManager_ControllerUnplugged;

            ManagerFactory.settingsManager.Initialized -= SettingsManager_Initialized;
            ManagerFactory.settingsManager.SettingValueChanged -= SettingsManager_SettingValueChanged;

            IsInitialized = false;

            LogManager.LogInformation("{0} has stopped", "SensorsManager");
        }

        public static void Suspend(bool OS)
        {
            if (!IsInitialized)
                return;

            StopListening();

            // close serial sensor, if any (avoid stale handles across suspend)
            if (sensorFamily == SensorFamily.SerialUSBIMU)
                USBSensor?.Close();
        }

        public static void Resume(bool OS)
        {
            // If we were fully stopped, ensure we are started again.
            if (!IsInitialized)
            {
                Start();
                return;
            }

            // Re-open serial sensor if it is currently selected
            if (sensorFamily == SensorFamily.SerialUSBIMU)
            {
                USBSensor = SerialUSBIMU.GetCurrent();
                USBSensor?.Open();
            }

            Gyrometer?.UpdateSensor();
            Accelerometer?.UpdateSensor();
        }

        private static void ControllerManager_ControllerSelected(IController? Controller)
        {
            if (Controller is null)
                return;

            if (!Controller.HasMotionSensor())
                return;

            ApplyCalibrationMode(Controller);

            if (sensorSelection == SensorFamily.Auto)
                PickNextSensor(SensorFamily.Controller);
            else if (sensorSelection == SensorFamily.Controller)
                ActivateSensor(SensorFamily.Controller);
        }

        private static void ControllerManager_ControllerUnplugged(IController Controller, bool IsPowerCycling, bool WasTarget)
        {
            if (sensorSelection != SensorFamily.Auto && sensorSelection != SensorFamily.Controller)
                return;

            // skip if controller isn't current or doesn't have motion sensor anyway
            if (!Controller.HasMotionSensor() || !WasTarget)
                return;

            if (sensorSelection == SensorFamily.Auto)
                PickNextSensor();
            else
                StopListening();
        }

        private static void DeviceManager_UsbDeviceRemoved(PnPDevice? device, Guid IntefaceGuid)
        {
            if (USBSensor is null)
                return;

            // If the USB Gyro is unplugged, close serial connection
            USBSensor.Close();
            USBSensor = null;

            if (sensorSelection != SensorFamily.Auto && sensorSelection != SensorFamily.SerialUSBIMU)
                return;

            if (sensorSelection == SensorFamily.Auto)
                PickNextSensor();
            else
                StopListening();
        }

        private static void DeviceManager_UsbDeviceArrived(PnPDevice? device, Guid IntefaceGuid)
        {
            // If USB Gyro is plugged, hook into it
            USBSensor = SerialUSBIMU.GetCurrent();

            if (USBSensor is null)
                return;

            if (sensorSelection == SensorFamily.Auto)
                PickNextSensor(SensorFamily.SerialUSBIMU);
            else if (sensorSelection == SensorFamily.SerialUSBIMU)
                ActivateSensor(SensorFamily.SerialUSBIMU);
        }

        private static void PickNextSensor(SensorFamily preferred = SensorFamily.None)
        {
            if (sensorSelection != SensorFamily.Auto)
                return;

            IController? controller = ControllerManager.GetTarget();
            bool hasControllerSensor = controller?.HasMotionSensor() ?? false;
            bool hasInternalSensor = IDevice.GetCurrent().Capabilities.HasFlag(DeviceCapabilities.InternalSensor);
            bool hasExternalSensor = USBSensor is not null;

            if (preferred == SensorFamily.Controller && hasControllerSensor)
                ActivateSensor(SensorFamily.Controller);
            else if (preferred == SensorFamily.SerialUSBIMU && hasExternalSensor)
                ActivateSensor(SensorFamily.SerialUSBIMU);
            else if (hasControllerSensor)
                ActivateSensor(SensorFamily.Controller);
            else if (hasInternalSensor)
                ActivateSensor(SensorFamily.Windows);
            else if (hasExternalSensor)
                ActivateSensor(SensorFamily.SerialUSBIMU);
        }

        private static void ActivateSensor(SensorFamily selectedFamily)
        {
            if (sensorFamily == selectedFamily)
                return;

            StopListening();
            if (sensorFamily == SensorFamily.SerialUSBIMU)
                USBSensor?.Close();

            sensorFamily = selectedFamily;

            if (sensorFamily == SensorFamily.SerialUSBIMU)
            {
                USBSensor ??= SerialUSBIMU.GetCurrent();
                if (USBSensor is null)
                    return;

                SerialPlacement placement = (SerialPlacement)ManagerFactory.settingsManager.GetInt("SensorPlacement");
                bool upsidedown = ManagerFactory.settingsManager.GetBoolean("SensorPlacementUpsideDown");
                USBSensor.Open();
                USBSensor.SetSensorPlacement(placement);
                USBSensor.SetSensorOrientation(upsidedown);
            }

            SetSensorFamily(sensorFamily);
            SensorSelectionChanged?.Invoke(sensorFamily);
        }

        private static void SettingsManager_SettingValueChanged(string name, object? value, bool temporary, bool initializing)
        {
            switch (name)
            {
                case "SensorPlacement":
                    {
                        SerialPlacement placement = (SerialPlacement)Convert.ToInt32(value);
                        USBSensor?.SetSensorPlacement(placement);
                    }
                    break;
                case "SensorPlacementUpsideDown":
                    {
                        bool upsidedown = Convert.ToBoolean(value);
                        USBSensor?.SetSensorOrientation(upsidedown);
                    }
                    break;
                case "SensorSelection":
                    {
                        int selectedValue = Convert.ToInt32(value);
                        SensorFamily selectedFamily = selectedValue == -1 ? SensorFamily.Auto : (SensorFamily)selectedValue;

                        // skip if set already
                        if (sensorSelection == selectedFamily)
                            return;

                        sensorSelection = selectedFamily;

                        if (sensorSelection == SensorFamily.Auto)
                            PickNextSensor();
                        else if (sensorSelection == SensorFamily.None)
                            ActivateSensor(SensorFamily.None);
                        else
                            ActivateSensor(sensorSelection);
                    }
                    break;
                case "SensorCalibrationMode":
                    {
                        CalibrationMode selectedMode = Convert.ToInt32(value) == (int)(CalibrationMode.Stillness | CalibrationMode.SensorFusion)
                            ? CalibrationMode.Stillness | CalibrationMode.SensorFusion
                            : CalibrationMode.Manual;

                        if (calibrationMode == selectedMode)
                            return;

                        calibrationMode = selectedMode;
                        ApplyCalibrationMode();
                        CalibrationModeChanged?.Invoke(calibrationMode);
                    }
                    break;
            }
        }

        private static void ApplyCalibrationMode()
        {
            ApplyCalibrationMode(IDevice.GetCurrent().GamepadMotion);

            if (ControllerManager.GetTarget() is IController controller)
                ApplyCalibrationMode(controller);
        }

        private static void ApplyCalibrationMode(IController controller)
        {
            foreach (GamepadMotion gamepadMotion in controller.gamepadMotions.Values)
                ApplyCalibrationMode(gamepadMotion);
        }

        private static void ApplyCalibrationMode(GamepadMotion gamepadMotion)
        {
            gamepadMotion.SetCalibrationMode(calibrationMode);
        }

        private static void StopListening()
        {
            Gyrometer?.StopListening();
            Accelerometer?.StopListening();
        }

        public static void UpdateReport(ControllerState controllerState, GamepadMotion gamepadMotion, ref float delta)
        {
            Vector3 accel = Accelerometer is not null ? Accelerometer.GetCurrentReading().reading : Vector3.Zero;
            Vector3 gyro = Gyrometer is not null ? Gyrometer.GetCurrentReading().reading : Vector3.Zero;

            // store motion
            controllerState.GyroState.SetGyroscope(gyro.X, gyro.Y, gyro.Z);
            controllerState.GyroState.SetAccelerometer(accel.X, accel.Y, accel.Z);

            // process motion
            gamepadMotion.ProcessMotion(gyro.X, gyro.Y, gyro.Z, accel.X, accel.Y, accel.Z, delta);
        }

        public static void SetSensorFamily(SensorFamily sensorFamily)
        {
            // initialize sensors
            int UpdateInterval = TimerManager.GetPeriod();

            Gyrometer = new IMUGyrometer(sensorFamily, UpdateInterval, IDevice.GetCurrent().GamepadMotion.GetCalibration().GetGyroThreshold());
            Accelerometer = new IMUAccelerometer(sensorFamily, UpdateInterval);
        }

        public static async void Calibrate(GamepadMotion gamepadMotion)
        {
            Calibrate(new Dictionary<byte, GamepadMotion> { { 0, gamepadMotion } });
        }

        public static async void Calibrate(Dictionary<byte, GamepadMotion> gamepadMotions)
        {
            if (calibrationMode != CalibrationMode.Manual)
                return;

            Dialog dialog = new Dialog(MainWindow.GetCurrent())
            {
                Title = "Please place the controller on a stable and level surface.",
                Content = string.Empty,
                CanClose = false
            };

            // display calibration dialog
            dialog.ShowAsync();

            // skip if empty
            if (gamepadMotions.Count == 0)
                goto Close;

            for (int i = 4; i > 0; i--)
            {
                dialog.UpdateContent($"Calibration will start in {i} seconds.");
                await Task.Delay(1000); // Captures synchronization context
            }

            foreach (GamepadMotion gamepadMotion in gamepadMotions.Values)
            {
                dialog.UpdateContent($"Calibrating {gamepadMotion.deviceInstanceId} stationary sensor noise and drift correction...");

                gamepadMotion.SetCalibrationMode(CalibrationMode.Manual);
                gamepadMotion.ResetContinuousCalibration();
                gamepadMotion.StartContinuousCalibration();
                await Task.Delay(TimeSpan.FromSeconds(5));
                gamepadMotion.PauseContinuousCalibration();

                // get/set calibration offsets
                gamepadMotion.GetCalibrationOffset(out float xOffset, out float yOffset, out float zOffset);
                gamepadMotion.SetCalibrationOffset(xOffset, yOffset, zOffset, 1);

                // store calibration offsets
                IMUCalibration.StoreCalibration(gamepadMotion.deviceInstanceId, gamepadMotion.GetCalibration());

                // display message
                dialog.UpdateContent("Calibration succeeded: stationary sensor noise recorded. Drift correction found.");

                // wait a bit
                await Task.Delay(2000); // Captures synchronization context
            }

        Close:
            dialog.Hide();
        }
    }
}