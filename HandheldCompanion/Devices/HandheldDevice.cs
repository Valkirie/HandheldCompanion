using ControllerCommon;
using ControllerCommon.Utils;
using HelixToolkit.Wpf;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using Windows.Devices.Sensors;
using static ControllerCommon.Utils.ProcessUtils;
using Brush = System.Windows.Media.Brush;
using Color = System.Windows.Media.Color;
using ColorConverter = System.Drawing.ColorConverter;
using GamepadButtonFlags = SharpDX.XInput.GamepadButtonFlags;

namespace HandheldCompanion.Devices
{
    public abstract class HandheldDevice
    {
        public USBDeviceInfo sensor = new USBDeviceInfo("0", "N/A", "");
        public bool sensorSupported;

        public string ManufacturerName;
        public string ProductName;
        public string ModelName;

        public bool hasGyrometer;
        public bool hasAccelerometer;
        public bool hasInclinometer;

        // Model3D vars
        public Model3DGroup model3DGroup = new Model3DGroup();
        protected ModelImporter modelImporter = new ModelImporter();

        // Common groups
        public Model3DGroup JoystickLeftRing;
        public Model3DGroup JoystickRightRing;
        public Model3DGroup MainBody;
        public Model3DGroup Screen;
        public Model3DGroup ShoulderLeftMiddle;
        public Model3DGroup ShoulderLeftTrigger;
        public Model3DGroup ShoulderRightMiddle;
        public Model3DGroup ShoulderRightTrigger;

        public Dictionary<GamepadButtonFlags, Model3DGroup> ButtonMap = new();

        // Rotation Points
        public Vector3D JoystickRotationPointCenterLeftMillimeter;
        public Vector3D JoystickRotationPointCenterRightMillimeter;
        public float JoystickMaxAngleDeg;
        public Vector3D ShoulderTriggerRotationPointCenterLeftMillimeter;
        public Vector3D ShoulderTriggerRotationPointCenterRightMillimeter;
        public float TriggerMaxAngleDeg;

        // Default Materials
        public Color ColorPlasticBlack;
        public Color ColorPlasticWhite;
        public Brush ColorHighlight;

        public DiffuseMaterial MaterialPlasticBlack;
        public DiffuseMaterial MaterialPlasticWhite;
        public DiffuseMaterial MaterialHighlight;

        protected HandheldDevice(string ManufacturerName, string ProductName, string ModelName)
        {
            this.ManufacturerName = ManufacturerName;
            this.ProductName = ProductName;
            this.ModelName = ModelName;

            // load model(s)
            JoystickLeftRing = modelImporter.Load($"models/{ModelName}/Joystick-Left-Ring.obj");
            JoystickRightRing = modelImporter.Load($"models/{ModelName}/Joystick-Right-Ring.obj");
            MainBody = modelImporter.Load($"models/{ModelName}/MainBody.obj");
            Screen = modelImporter.Load($"models/{ModelName}/Screen.obj");
            ShoulderLeftTrigger = modelImporter.Load($"models/{ModelName}/Shoulder-Left-Trigger.obj");
            ShoulderRightTrigger = modelImporter.Load($"models/{ModelName}/Shoulder-Right-Trigger.obj");

            // map model(s)
            foreach(GamepadButtonFlags button in Enum.GetValues(typeof(GamepadButtonFlags)))
            {
                string filename = $"models/{ModelName}/{button}.obj";
                if (File.Exists(filename))
                {
                    Model3DGroup model = modelImporter.Load(filename);
                    ButtonMap.Add(button, model);

                    // pull model
                    model3DGroup.Children.Add(model);
                }
            }

            // pull model(s)
            model3DGroup.Children.Add(JoystickLeftRing);
            model3DGroup.Children.Add(JoystickRightRing);
            model3DGroup.Children.Add(MainBody);
            model3DGroup.Children.Add(Screen);
            model3DGroup.Children.Add(ShoulderLeftTrigger);
            model3DGroup.Children.Add(ShoulderRightTrigger);

            Gyrometer gyrometer = Gyrometer.GetDefault();
            if (gyrometer != null)
            {
                hasGyrometer = true;

                string ACPI = CommonUtils.Between(gyrometer.DeviceId, "ACPI#", "#");

                sensor = GetUSBDevices().FirstOrDefault(device => device.DeviceId.Contains(ACPI));
                if (sensor != null)
                {
                    switch (sensor.Name)
                    {
                        case "BMI160":
                            sensorSupported = true;
                            break;
                    }
                }
            }

            Accelerometer accelerometer = Accelerometer.GetDefault();
            if (accelerometer != null)
                hasAccelerometer = true;

            Inclinometer inclinometer = Inclinometer.GetDefault();
            if (inclinometer != null)
                hasInclinometer = true;
        }
    }
}
