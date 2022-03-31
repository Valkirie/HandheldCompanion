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
        public Model3DGroup DPadDown;
        public Model3DGroup DPadLeft;
        public Model3DGroup DPadRight;
        public Model3DGroup DPadUp;
        public Model3DGroup FaceButtonA;
        public Model3DGroup FaceButtonB;
        public Model3DGroup FaceButtonX;
        public Model3DGroup FaceButtonY;
        public Model3DGroup JoystickLeftRing;
        public Model3DGroup JoystickLeftStick;
        public Model3DGroup JoystickRightRing;
        public Model3DGroup JoystickRightStick;
        public Model3DGroup MainBody;
        public Model3DGroup Screen;
        public Model3DGroup ShoulderLeftButton;
        public Model3DGroup ShoulderLeftMiddle;
        public Model3DGroup ShoulderLeftTrigger;
        public Model3DGroup ShoulderRightButton;
        public Model3DGroup ShoulderRightMiddle;
        public Model3DGroup ShoulderRightTrigger;
        public Model3DGroup Start;
        public Model3DGroup Back;

        public Dictionary<GamepadButtonFlags, Model3DGroup> ButtonMap;
        protected Dictionary<Model3DGroup, string> ModelMap;

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

            // load models
            DPadDown = modelImporter.Load($"models/{ModelName}/DPad-Down.obj");
            DPadLeft = modelImporter.Load($"models/{ModelName}/DPad-Left.obj");
            DPadRight = modelImporter.Load($"models/{ModelName}/DPad-Right.obj");
            DPadUp = modelImporter.Load($"models/{ModelName}/DPad-Up.obj");
            FaceButtonA = modelImporter.Load($"models/{ModelName}/FaceButton-A.obj");
            FaceButtonB = modelImporter.Load($"models/{ModelName}/FaceButton-B.obj");
            FaceButtonX = modelImporter.Load($"models/{ModelName}/FaceButton-X.obj");
            FaceButtonY = modelImporter.Load($"models/{ModelName}/FaceButton-Y.obj");
            JoystickLeftStick = modelImporter.Load($"models/{ModelName}/Joystick-Left-Stick.obj");
            JoystickRightStick = modelImporter.Load($"models/{ModelName}/Joystick-Right-Stick.obj");
            ShoulderLeftButton = modelImporter.Load($"models/{ModelName}/Shoulder-Left-Button.obj");
            ShoulderRightButton = modelImporter.Load($"models/{ModelName}/Shoulder-Right-Button.obj");
            Back = modelImporter.Load($"models/{ModelName}/WFB-View.obj");
            Start = modelImporter.Load($"models/{ModelName}/WFB-Menu.obj");

            // load models
            JoystickLeftRing = modelImporter.Load($"models/{ModelName}/Joystick-Left-Ring.obj");
            JoystickRightRing = modelImporter.Load($"models/{ModelName}/Joystick-Right-Ring.obj");
            MainBody = modelImporter.Load($"models/{ModelName}/MainBody.obj");
            Screen = modelImporter.Load($"models/{ModelName}/Screen.obj");
            ShoulderLeftTrigger = modelImporter.Load($"models/{ModelName}/Shoulder-Left-Trigger.obj");
            ShoulderRightTrigger = modelImporter.Load($"models/{ModelName}/Shoulder-Right-Trigger.obj");

            // map models
            ButtonMap = new Dictionary<GamepadButtonFlags, Model3DGroup>
            {
                { GamepadButtonFlags.DPadUp, DPadUp },
                { GamepadButtonFlags.DPadLeft, DPadLeft },
                { GamepadButtonFlags.DPadRight, DPadRight },
                { GamepadButtonFlags.DPadDown, DPadDown },

                { GamepadButtonFlags.A, FaceButtonA },
                { GamepadButtonFlags.B, FaceButtonB },
                { GamepadButtonFlags.X, FaceButtonX },
                { GamepadButtonFlags.Y, FaceButtonY },

                { GamepadButtonFlags.LeftThumb, JoystickLeftStick },
                { GamepadButtonFlags.RightThumb, JoystickRightStick },

                { GamepadButtonFlags.LeftShoulder, ShoulderLeftButton },
                { GamepadButtonFlags.RightShoulder, ShoulderRightButton },

                { GamepadButtonFlags.Start, Start },
                { GamepadButtonFlags.Back, Back },
            };

            // pull models
            model3DGroup.Children.Add(DPadDown);
            model3DGroup.Children.Add(DPadLeft);
            model3DGroup.Children.Add(DPadRight);
            model3DGroup.Children.Add(DPadUp);
            model3DGroup.Children.Add(FaceButtonA);
            model3DGroup.Children.Add(FaceButtonB);
            model3DGroup.Children.Add(FaceButtonX);
            model3DGroup.Children.Add(FaceButtonY);
            model3DGroup.Children.Add(JoystickLeftRing);
            model3DGroup.Children.Add(JoystickLeftStick);
            model3DGroup.Children.Add(JoystickRightRing);
            model3DGroup.Children.Add(JoystickRightStick);
            model3DGroup.Children.Add(MainBody);
            model3DGroup.Children.Add(Screen);
            model3DGroup.Children.Add(ShoulderLeftButton);
            model3DGroup.Children.Add(ShoulderLeftTrigger);
            model3DGroup.Children.Add(ShoulderRightButton);
            model3DGroup.Children.Add(ShoulderRightTrigger);
            model3DGroup.Children.Add(Start);
            model3DGroup.Children.Add(Back);

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
