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
        private static IMUGyrometer Gyrometer;
        private static IMUAccelerometer Accelerometer;
        private static SerialUSBIMU USBSensor;

        private static SensorFamily sensorFamily;

        public static bool IsInitialized;

        public static event InitializedEventHandler Initialized;
        public delegate void InitializedEventHandler();

        static SensorsManager()
        {
        }

        public static void Start()
        {
            if (IsInitialized)
                return;

            // manage events
            ManagerFactory.deviceManager.UsbDeviceArrived += DeviceManager_UsbDeviceArrived;
            ManagerFactory.deviceManager.UsbDeviceRemoved += DeviceManager_UsbDeviceRemoved;
            ControllerManager.ControllerSelected += ControllerManager_ControllerSelected;
            ControllerManager.ControllerUnplugged += ControllerManager_ControllerUnplugged;

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

            if (ManagerFactory.deviceManager.IsRunning)
            {
                DeviceManager_UsbDeviceArrived(null, Guid.Empty);
            }

            if (ControllerManager.HasTargetController)
                ControllerManager_ControllerSelected(ControllerManager.GetTarget());

            IsInitialized = true;
            Initialized?.Invoke();

            LogManager.LogInformation("{0} has started", "SensorsManager");
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
            SettingsManager_SettingValueChanged("SensorPlacement", ManagerFactory.settingsManager.GetString("SensorPlacement"), false);
            SettingsManager_SettingValueChanged("SensorPlacementUpsideDown", ManagerFactory.settingsManager.GetString("SensorPlacementUpsideDown"), false);
            SettingsManager_SettingValueChanged("SensorSelection", ManagerFactory.settingsManager.GetString("SensorSelection"), false);
        }

        public static void Stop()
        {
            if (!IsInitialized)
                return;

            StopListening();

            // manage events
            ManagerFactory.deviceManager.UsbDeviceArrived -= DeviceManager_UsbDeviceArrived;
            ManagerFactory.deviceManager.UsbDeviceRemoved -= DeviceManager_UsbDeviceRemoved;
            ControllerManager.ControllerSelected -= ControllerManager_ControllerSelected;
            ControllerManager.ControllerUnplugged -= ControllerManager_ControllerUnplugged;
            ManagerFactory.settingsManager.SettingValueChanged -= SettingsManager_SettingValueChanged;
            ManagerFactory.settingsManager.Initialized -= SettingsManager_Initialized;

            IsInitialized = false;

            LogManager.LogInformation("{0} has stopped", "SensorsManager");
        }

        private static void ControllerManager_ControllerSelected(IController Controller)
        {
            if (Controller is null)
                return;

            // select controller as current sensor if current sensor selection is none
            if (Controller.Capabilities.HasFlag(ControllerCapabilities.MotionSensor))
                ManagerFactory.settingsManager.SetProperty("SensorSelection", (int)SensorFamily.Controller);
            else
                PickNextSensor();
        }

        private static void ControllerManager_ControllerUnplugged(IController Controller, bool IsPowerCycling, bool WasTarget)
        {
            if (sensorFamily != SensorFamily.Controller)
                return;

            // skip if controller isn't current or doesn't have motion sensor anyway
            if (!Controller.HasMotionSensor() || !WasTarget)
                return;

            // pick next available sensor
            PickNextSensor();
        }

        private static void DeviceManager_UsbDeviceRemoved(PnPDevice device, Guid IntefaceGuid)
        {
            if (USBSensor is null || sensorFamily != SensorFamily.SerialUSBIMU)
                return;

            // If the USB Gyro is unplugged, close serial connection
            USBSensor.Close();

            // pick next available sensor
            PickNextSensor();
        }

        private static void PickNextSensor()
        {
            // get current controller
            IController? controller = ControllerManager.GetTarget();

            if (controller is not null && controller.HasMotionSensor())
                ManagerFactory.settingsManager.SetProperty("SensorSelection", (int)SensorFamily.Controller);
            else if (IDevice.GetCurrent().Capabilities.HasFlag(DeviceCapabilities.InternalSensor))
                ManagerFactory.settingsManager.SetProperty("SensorSelection", (int)SensorFamily.Windows);
            else if (IDevice.GetCurrent().Capabilities.HasFlag(DeviceCapabilities.ExternalSensor))
                ManagerFactory.settingsManager.SetProperty("SensorSelection", (int)SensorFamily.SerialUSBIMU);
            else
                ManagerFactory.settingsManager.SetProperty("SensorSelection", (int)SensorFamily.None);
        }

        private static void DeviceManager_UsbDeviceArrived(PnPDevice device, Guid IntefaceGuid)
        {
            // If USB Gyro is plugged, hook into it
            USBSensor = SerialUSBIMU.GetCurrent();

            // select serial usb as current sensor if current sensor selection is none
            if (sensorFamily == SensorFamily.None)
                ManagerFactory.settingsManager.SetProperty("SensorSelection", (int)SensorFamily.SerialUSBIMU);
        }

        private static void SettingsManager_SettingValueChanged(string name, object value, bool temporary)
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
                        SensorFamily sensorSelection = (SensorFamily)Convert.ToInt32(value);

                        // skip if set already
                        if (sensorFamily == sensorSelection)
                            return;

                        switch (sensorFamily)
                        {
                            case SensorFamily.Windows:
                                StopListening();
                                break;

                            case SensorFamily.SerialUSBIMU:
                                USBSensor?.Close();
                                break;
                        }

                        // update current sensorFamily
                        sensorFamily = sensorSelection;

                        switch (sensorFamily)
                        {
                            case SensorFamily.SerialUSBIMU:
                                {
                                    // get current USB sensor
                                    USBSensor = SerialUSBIMU.GetCurrent();
                                    if (USBSensor is null)
                                    {
                                        PickNextSensor();
                                        break;
                                    }

                                    SerialPlacement placement = (SerialPlacement)ManagerFactory.settingsManager.GetInt("SensorPlacement");
                                    bool upsidedown = ManagerFactory.settingsManager.GetBoolean("SensorPlacementUpsideDown");

                                    USBSensor.Open();
                                    USBSensor.SetSensorPlacement(placement);
                                    USBSensor.SetSensorOrientation(upsidedown);
                                }
                                break;

                            case SensorFamily.Controller:
                                {
                                    // get current controller
                                    IController controller = ControllerManager.GetTarget();
                                    if (controller is null || !controller.Capabilities.HasFlag(ControllerCapabilities.MotionSensor))
                                    {
                                        PickNextSensor();
                                        break;
                                    }
                                }
                                break;
                        }

                        SetSensorFamily(sensorSelection);
                    }
                    break;
            }
        }

        public static void Resume(bool OS)
        {
            Gyrometer?.UpdateSensor();
            Accelerometer?.UpdateSensor();
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

                gamepadMotion.ResetContinuousCalibration();
                gamepadMotion.SetCalibrationMode(CalibrationMode.Stillness | CalibrationMode.SensorFusion);

                // wait until device is steady
                DateTime timeout = DateTime.Now.Add(TimeSpan.FromSeconds(5));
                float confidence = 0.0f;

                while (DateTime.Now < timeout)
                {
                    confidence = gamepadMotion.GetAutoCalibrationConfidence();
                    if (confidence == 1.0f)
                        break;

                    await Task.Delay(10);
                }

                // get/set calibration offsets
                gamepadMotion.GetCalibrationOffset(out float xOffset, out float yOffset, out float zOffset);
                gamepadMotion.SetCalibrationOffset(xOffset, yOffset, zOffset, (int)(confidence * 10.0f));

                // store calibration offsets
                IMUCalibration.StoreCalibration(gamepadMotion.deviceInstanceId, gamepadMotion.GetCalibration());

                // restore calibration mode
                gamepadMotion.SetCalibrationMode(CalibrationMode.Manual);

                // display message
                dialog.UpdateContent($"Calibration succeeded: stationary sensor noise recorded. Drift correction found. Confidence: {confidence * 100.0f}%");

                // wait a bit
                await Task.Delay(2000); // Captures synchronization context
            }

        Close:
            dialog.Hide();
        }
    }
}