using HandheldCompanion.Controllers;
using HandheldCompanion.Devices;
using HandheldCompanion.Sensors;
using HandheldCompanion.Views;
using Nefarius.Utilities.DeviceManagement.PnP;
using System;
using Windows.UI.Popups;
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
            DeviceManager.UsbDeviceArrived += DeviceManager_UsbDeviceArrived;
            DeviceManager.UsbDeviceRemoved += DeviceManager_UsbDeviceRemoved;

            ControllerManager.ControllerSelected += ControllerManager_ControllerSelected;
            ControllerManager.ControllerUnplugged += ControllerManager_ControllerUnplugged;

            SettingsManager.SettingValueChanged += SettingsManager_SettingValueChanged;
        }

        private static void ControllerManager_ControllerSelected(IController Controller)
        {
            // select controller as current sensor if current sensor selection is none
            if (sensorFamily == SensorFamily.None && Controller.Capabilities.HasFlag(ControllerCapabilities.MotionSensor))
                SettingsManager.SetProperty("SensorSelection", (int)SensorFamily.Controller);
        }

        private static void ControllerManager_ControllerUnplugged(IController Controller)
        {
            if (sensorFamily != SensorFamily.Controller)
                return;

            // skip if controller isn't current or doesn't have motion sensor anyway
            if (!Controller.HasMotionSensor() || Controller != ControllerManager.GetTargetController())
                return;
            
            if (sensorFamily == SensorFamily.Controller)
                PickNextSensor();
        }

        private static void DeviceManager_UsbDeviceRemoved(PnPDevice device, DeviceEventArgs obj)
        {
            if (USBSensor is null)
                return;

            // If the USB Gyro is unplugged, close serial connection
            USBSensor.Close();

            if (sensorFamily == SensorFamily.SerialUSBIMU)
                PickNextSensor();
        }
        
        private static void PickNextSensor()
        {
            if (MainWindow.CurrentDevice.Capabilities.HasFlag(DeviceCapabilities.InternalSensor))
                SettingsManager.SetProperty("SensorSelection", (int)SensorFamily.Windows);
            else if (MainWindow.CurrentDevice.Capabilities.HasFlag(DeviceCapabilities.ExternalSensor))
                SettingsManager.SetProperty("SensorSelection", (int)SensorFamily.SerialUSBIMU);
            else if (ControllerManager.GetTargetController() is not null && ControllerManager.GetTargetController().HasMotionSensor())
                SettingsManager.SetProperty("SensorSelection", (int)SensorFamily.Controller);
            else
                SettingsManager.SetProperty("SensorSelection", (int)SensorFamily.None);
        }

        private static void DeviceManager_UsbDeviceArrived(PnPDevice device, DeviceEventArgs obj)
        {
            // If USB Gyro is plugged, hook into it
            USBSensor = SerialUSBIMU.GetDefault();

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
                                    USBSensor = SerialUSBIMU.GetDefault();

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
            if (Gyrometer is not null)
                Gyrometer.UpdateSensor();

            if (Accelerometer is not null)
                Accelerometer.UpdateSensor();
        }

        private static void StopListening()
        {
            // if required, halt gyrometer
            if (Gyrometer is not null)
                Gyrometer.StopListening();

            // if required, halt accelerometer
            if (Accelerometer is not null)
                Accelerometer?.StopListening();
        }

        public static void UpdateReport(ControllerState controllerState)
        {
            switch(sensorFamily)
            {
                case SensorFamily.None:
                case SensorFamily.Controller:
                    return;
            }

            if (Gyrometer is not null)
                controllerState.GyroState.Gyroscope = Gyrometer.GetCurrentReading();

            if (Accelerometer is not null)
                controllerState.GyroState.Accelerometer = Accelerometer.GetCurrentReading();
        }

        public static void SetSensorFamily(SensorFamily sensorFamily)
        {
            // initialize sensors
            var UpdateInterval = TimerManager.GetPeriod();

            Gyrometer = new IMUGyrometer(sensorFamily, UpdateInterval);
            Accelerometer = new IMUAccelerometer(sensorFamily, UpdateInterval);
        }
    }
}
