using HandheldCompanion.Controllers;
using HandheldCompanion.Devices;
using HandheldCompanion.Helpers;
using HandheldCompanion.Misc;
using HandheldCompanion.Sensors;
using HandheldCompanion.Views;
using Nefarius.Utilities.DeviceManagement.PnP;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
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
        public static GamepadMotion GamepadMotion;

        private static SensorFamily sensorFamily;

        private static Dictionary<string, IMUCalibration> Calibrations = new();
        public static string CalibrationPath;

        public static bool IsInitialized;

        public static event InitializedEventHandler Initialized;
        public delegate void InitializedEventHandler();

        static SensorsManager()
        {
            // initialiaze path
            CalibrationPath = Path.Combine(MainWindow.SettingsPath, "calibration.json");
            Calibrations = DeserializeCollection();

            DeviceManager.UsbDeviceArrived += DeviceManager_UsbDeviceArrived;
            DeviceManager.UsbDeviceRemoved += DeviceManager_UsbDeviceRemoved;

            ControllerManager.ControllerSelected += ControllerManager_ControllerSelected;
            ControllerManager.ControllerUnplugged += ControllerManager_ControllerUnplugged;

            SettingsManager.SettingValueChanged += SettingsManager_SettingValueChanged;
        }

        private static void ControllerManager_ControllerSelected(IController Controller)
        {
            // select controller as current sensor if current sensor selection is none
            if (Controller.Capabilities.HasFlag(ControllerCapabilities.MotionSensor))
                SettingsManager.SetProperty("SensorSelection", (int)SensorFamily.Controller);
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

        private static void DeviceManager_UsbDeviceRemoved(PnPDevice device, DeviceEventArgs obj)
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
            if (ControllerManager.GetTargetController() is not null && ControllerManager.GetTargetController().HasMotionSensor())
                SettingsManager.SetProperty("SensorSelection", (int)SensorFamily.Controller);
            else if (IDevice.GetCurrent().Capabilities.HasFlag(DeviceCapabilities.InternalSensor))
                SettingsManager.SetProperty("SensorSelection", (int)SensorFamily.Windows);
            else if (IDevice.GetCurrent().Capabilities.HasFlag(DeviceCapabilities.ExternalSensor))
                SettingsManager.SetProperty("SensorSelection", (int)SensorFamily.SerialUSBIMU);
            else
                SettingsManager.SetProperty("SensorSelection", (int)SensorFamily.None);
        }

        private static void DeviceManager_UsbDeviceArrived(PnPDevice device, DeviceEventArgs obj)
        {
            // If USB Gyro is plugged, hook into it
            USBSensor = SerialUSBIMU.GetCurrent();

            // select serial usb as current sensor if current sensor selection is none
            if (sensorFamily == SensorFamily.None)
                SettingsManager.SetProperty("SensorSelection", (int)SensorFamily.SerialUSBIMU);
        }

        private static void SettingsManager_SettingValueChanged(string name, object value)
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

                        switch(sensorFamily)
                        {
                            case SensorFamily.Windows:
                                StopListening();
                                break;
                            case SensorFamily.SerialUSBIMU:
                                if (USBSensor is not null)
                                    USBSensor.Close();
                                break;
                            case SensorFamily.Controller:
                                break;
                        }

                        // update current sensorFamily
                        sensorFamily = sensorSelection;

                        switch(sensorFamily)
                        {
                            case SensorFamily.Windows:
                                break;
                            case SensorFamily.SerialUSBIMU:
                                {
                                    USBSensor = SerialUSBIMU.GetCurrent();

                                    if (USBSensor is null)
                                        break;

                                    USBSensor.Open();

                                    SerialPlacement placement = (SerialPlacement)SettingsManager.GetInt("SensorPlacement");
                                    USBSensor.SetSensorPlacement(placement);
                                    bool upsidedown = SettingsManager.GetBoolean("SensorPlacementUpsideDown");
                                    USBSensor.SetSensorOrientation(upsidedown);
                                }
                                break;
                            case SensorFamily.Controller:
                                break;
                        }

                        SetSensorFamily(sensorSelection);
                    }
                    break;
            }
        }

        public static void SerializeCollection(Dictionary<string, IMUCalibration> collection)
        {
            string json = JsonConvert.SerializeObject(collection, Formatting.Indented);
            File.WriteAllText(CalibrationPath, json);
        }

        public static Dictionary<string, IMUCalibration> DeserializeCollection()
        {
            if (!File.Exists(CalibrationPath))
                return new();

            string json = File.ReadAllText(CalibrationPath);
            return JsonConvert.DeserializeObject<Dictionary<string, IMUCalibration>>(json);
        }

        public static IMUCalibration GetCalibration(string path)
        {
            if (Calibrations.TryGetValue(path, out IMUCalibration calibration))
            {
                LogManager.LogDebug("Restored calibration offsets for device: {0}", path);
                return calibration;
            }

            LogManager.LogDebug("No calibration offsets available for device: {0}", path);
            return new();
        }

        public static void StoreCalibration(string path, IMUCalibration calibration)
        {
            // upcase
            path = path.ToUpper();

            // update array
            Calibrations[path] = calibration;
            LogManager.LogDebug("Updated calibration offsets for device: {0}", path);

            // serialize to calibration.json
            SerializeCollection(Calibrations);
        }

        public static void Start()
        {
            IsInitialized = true;
            Initialized?.Invoke();

            LogManager.LogInformation("{0} has started", "SensorsManager");
        }

        public static void Stop()
        {
            if (!IsInitialized)
                return;

            StopListening();

            IsInitialized = false;

            LogManager.LogInformation("{0} has stopped", "SensorsManager");
        }

        public static void Resume(bool update)
        {
            Gyrometer?.UpdateSensor();
            Accelerometer?.UpdateSensor();
        }

        private static void StopListening()
        {
            Gyrometer?.StopListening();
            Accelerometer?.StopListening();
        }

        public static void UpdateReport(ControllerState controllerState, GamepadMotion gamepadMotion, float delta)
        {
            Vector3 accel = Accelerometer is not null ? Accelerometer.GetCurrentReading() : Vector3.Zero;
            Vector3 gyro = Gyrometer is not null ? Gyrometer.GetCurrentReading() : Vector3.Zero;

            // Store motion
            controllerState.GyroState.Gyroscope.X = gyro.X;
            controllerState.GyroState.Gyroscope.Y = gyro.Y;
            controllerState.GyroState.Gyroscope.Z = gyro.Z;
            controllerState.GyroState.Accelerometer.X = accel.X;
            controllerState.GyroState.Accelerometer.Y = accel.Y;
            controllerState.GyroState.Accelerometer.Z = accel.Z;

            // process motion
            gamepadMotion.ProcessMotion(controllerState.GyroState.Gyroscope.X, controllerState.GyroState.Gyroscope.Y, controllerState.GyroState.Gyroscope.Z, controllerState.GyroState.Accelerometer.X, controllerState.GyroState.Accelerometer.Y, controllerState.GyroState.Accelerometer.Z, delta);
        }

        public static void SetSensorFamily(SensorFamily sensorFamily)
        {
            // initialize sensors
            int UpdateInterval = TimerManager.GetPeriod();

            Gyrometer = new IMUGyrometer(sensorFamily, UpdateInterval);
            Accelerometer = new IMUAccelerometer(sensorFamily, UpdateInterval);

            switch(sensorFamily)
            {
                case SensorFamily.Windows:
                case SensorFamily.SerialUSBIMU:
                    GamepadMotion = new(Gyrometer.GetInstanceId());
                    GamepadMotion.SetCalibrationMode(CalibrationMode.Manual | CalibrationMode.SensorFusion);
                    break;
            }
        }

        public static async void Calibrate(GamepadMotion gamepadMotion)
        {
            Dialog dialog = new Dialog(MainWindow.GetCurrent())
            {
                Title = "Please place the controller on a stable and level surface.",
                Content = "Calibrating stationary sensor noise and drift correction...",
                CanClose = false
            };
            dialog.Show();

            // skip if null
            if (gamepadMotion is null)
                goto Failed;

            // reset motion values
            gamepadMotion.ResetMotion();

            // wait until device is steady
            DateTime timeout = DateTime.Now.Add(TimeSpan.FromSeconds(5));
            while (DateTime.Now < timeout && !gamepadMotion.GetAutoCalibrationIsSteady())
                await Task.Delay(100);

            bool IsSteady = gamepadMotion.GetAutoCalibrationIsSteady();
            if (!IsSteady)
                goto Failed;

            // force manual calibration
            gamepadMotion.StartContinuousCalibration();
            timeout = DateTime.Now.Add(TimeSpan.FromSeconds(3));
            while (DateTime.Now < timeout)
                await Task.Delay(100);

            gamepadMotion.PauseContinuousCalibration();
            goto Success;

        Failed:
            // display message
            dialog.UpdateContent("Calibration failed: device is too shaky.");
            goto Close;

        Success:
            float confidence = gamepadMotion.GetAutoCalibrationConfidence();
            gamepadMotion.GetCalibrationOffset(out float xOffset, out float yOffset, out float zOffset);
            IMUCalibration calibration = new(xOffset, yOffset, zOffset, (int)(confidence * 10.0f));
            StoreCalibration(gamepadMotion.deviceInstanceId, calibration);

            // display message
            dialog.UpdateContent($"Calibration succeeded: stationary sensor noise recorded. Drift correction found. Confidence: {confidence * 100.0f}%");
        
        Close:
            await Task.Delay(2000);
            dialog.Hide();
        }
    }
}