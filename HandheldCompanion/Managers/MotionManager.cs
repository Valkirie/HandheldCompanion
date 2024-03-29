using HandheldCompanion.Actions;
using HandheldCompanion.Controllers;
using HandheldCompanion.Devices;
using HandheldCompanion.Helpers;
using HandheldCompanion.Inputs;
using HandheldCompanion.Misc;
using HandheldCompanion.Sensors;
using HandheldCompanion.Utils;
using HandheldCompanion.Views;
using Microsoft.WindowsAPICodePack.Sensors;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Windows;
using static HandheldCompanion.Utils.DeviceUtils;
using static JSL;

namespace HandheldCompanion.Managers
{
    public static class MotionManager
    {
        public const byte SENSOR_DEFAULT = 0;
        public const byte SENSOR_GMH = 1;
        public const byte SENSOR_DSU = 2;
        public const byte SENSOR_MAX = 3;

        private static Vector3[] accelerometer = new Vector3[SENSOR_MAX];
        private static Vector3[] gyroscope = new Vector3[SENSOR_MAX];
        private static GyroActions gyroAction = new();

        private static Inclination inclination = new();

        private static IEnumerable<ButtonFlags> resetFlags = new List<ButtonFlags>() { ButtonFlags.B1, ButtonFlags.B2, ButtonFlags.B3, ButtonFlags.B4 };

        public static event SettingsMode0EventHandler SettingsMode0Update;
        public delegate void SettingsMode0EventHandler(Vector3 gyrometer);

        public static event SettingsMode1EventHandler SettingsMode1Update;
        public delegate void SettingsMode1EventHandler(Vector2 deviceAngle);

        public static bool IsInitialized;

        public static event InitializedEventHandler Initialized;
        public delegate void InitializedEventHandler();

        static MotionManager()
        {
            float samplePeriod = TimerManager.GetPeriod() / 1000f;
        }

        public static void Start()
        {
            IsInitialized = true;
            Initialized?.Invoke();
        }

        public static void Stop()
        {
            IsInitialized = false;
        }

        public static void UpdateReport(ControllerState controllerState, GamepadMotion gamepadMotion, float delta)
        {
            SetupMotion(controllerState, gamepadMotion, delta);
            ProcessMotion(controllerState, gamepadMotion, delta);
            
            if (controllerState.ButtonState.Buttons.Intersect(resetFlags).Count() == 4)
                gamepadMotion.ResetMotion();
        }

        // this function sets some basic motion settings, sensitivity and inverts
        // and is enough for DS4/DSU gyroscope handling
        private static void SetupMotion(ControllerState controllerState, GamepadMotion gamepadMotion, float delta)
        {
            Profile current = ProfileManager.GetCurrent();

            // get motion values
            gamepadMotion.GetCalibratedGyro(out float gyroX, out float gyroY, out float gyroZ);
            gamepadMotion.GetGravity(out float accelX, out float accelY, out float accelZ);
            
            // store GamepadMotionHelper values
            accelerometer[SENSOR_GMH] = new() { X = accelX, Y = accelY, Z = accelZ };
            gyroscope[SENSOR_GMH] = new() { X = gyroX, Y = gyroY, Z = gyroZ };

            // manage multiplier
            accelerometer[SENSOR_DEFAULT] = accelerometer[SENSOR_GMH] * current.AccelerometerMultiplier;
            gyroscope[SENSOR_DEFAULT] = gyroscope[SENSOR_GMH] * current.GyrometerMultiplier;

            // Assuming accelerometerData and gyroscopeData are Vector3 objects representing your sensor data
            switch(current.SteeringAxis)
            {
                case SteeringAxis.Yaw:
                    {
                        float tempZ = -accelerometer[SENSOR_DEFAULT].Z;
                        accelerometer[SENSOR_DEFAULT].Z = -accelerometer[SENSOR_DEFAULT].Y;
                        accelerometer[SENSOR_DEFAULT].Y = tempZ;

                        tempZ = -gyroscope[SENSOR_DEFAULT].Z;
                        gyroscope[SENSOR_DEFAULT].Z = -gyroscope[SENSOR_DEFAULT].Y;
                        gyroscope[SENSOR_DEFAULT].Y = tempZ;
                    }
                    break;
            }

            // prepare DSU
            accelerometer[SENSOR_DSU] = new() { X = accelerometer[SENSOR_DEFAULT].X, Y = accelerometer[SENSOR_DEFAULT].Y, Z = accelerometer[SENSOR_DEFAULT].Z };
            gyroscope[SENSOR_DSU] = new() { X = gyroscope[SENSOR_DEFAULT].X, Y = gyroscope[SENSOR_DEFAULT].Y, Z = gyroscope[SENSOR_DEFAULT].Z };

            if (current.MotionInvertHorizontal)
            {
                accelerometer[SENSOR_DSU].Y *= -1.0f;
                accelerometer[SENSOR_DSU].Z *= -1.0f;
                gyroscope[SENSOR_DSU].Y *= -1.0f;
                gyroscope[SENSOR_DSU].Z *= -1.0f;
            }

            if (current.MotionInvertVertical)
            {
                accelerometer[SENSOR_DSU].Y *= -1.0f;
                accelerometer[SENSOR_DSU].X *= -1.0f;
                gyroscope[SENSOR_DSU].Y *= -1.0f;
                gyroscope[SENSOR_DSU].X *= -1.0f;
            }

            // store current values, they are used by DS4 and DSU
            controllerState.GyroState.Accelerometer = accelerometer[SENSOR_DSU];
            controllerState.GyroState.Gyroscope = gyroscope[SENSOR_DSU];
        }

        // this function is used for advanced motion calculations used by
        // gyro to joy/mouse mappings, by UI that configures them and by 3D overlay
        private static void ProcessMotion(ControllerState controllerState, GamepadMotion gamepadMotion, float delta)
        {
            Layout currentLayout = LayoutManager.GetCurrent();
            if (currentLayout.GyroLayout.TryGetValue(AxisLayoutFlags.Gyroscope, out IActions action))
                if (action is not null)
                    gyroAction = action as GyroActions;

            //toggle motion when trigger is pressed
            if (gyroAction.MotionMode == MotionMode.Toggle)
            {
                if (gyroAction.MotionTogglePressed)
                {
                    if (!controllerState.ButtonState.ContainsTrue(gyroAction.MotionTrigger))
                    {
                        gyroAction.MotionTogglePressed = false; // disable debounce flag
                    }
                }
                else
                {
                    if (controllerState.ButtonState.ContainsTrue(gyroAction.MotionTrigger))
                    {
                        gyroAction.MotionToggleStatus = !gyroAction.MotionToggleStatus;
                        gyroAction.MotionTogglePressed = true; // enable debounce flag
                    }
                }
            }

            // check if motion input is active
            bool MotionTriggered =
                (gyroAction.MotionMode == MotionMode.Off && controllerState.ButtonState.ContainsTrue(gyroAction.MotionTrigger)) ||
                (gyroAction.MotionMode == MotionMode.On && !controllerState.ButtonState.ContainsTrue(gyroAction.MotionTrigger)) ||
                (gyroAction.MotionMode == MotionMode.Toggle && gyroAction.MotionToggleStatus);

            bool MotionMapped = action?.actionType != ActionType.Disabled;

            // update inclination only when needed
            if ((MotionMapped && MotionTriggered && gyroAction.MotionInput == MotionInput.JoystickSteering) || MainWindow.CurrentPageName == "SettingsMode1")
                inclination.UpdateReport(accelerometer[SENSOR_DEFAULT]);

            switch (MainWindow.CurrentPageName)
            {
                case "SettingsMode0":
                    SettingsMode0Update?.Invoke(gyroscope[SENSOR_DEFAULT]);
                    break;
                case "SettingsMode1":
                    SettingsMode1Update?.Invoke(inclination.Angles);
                    break;
            }

            // after this point the code only makes sense if we're actively using mapped gyro
            // if we are not, nullify the last state to remove drift
            if (!MotionTriggered || !MotionMapped)
            {
                controllerState.AxisState[AxisFlags.GyroX] = 0;
                controllerState.AxisState[AxisFlags.GyroY] = 0;
                return;
            }

            Profile currentProfile = ProfileManager.GetCurrent();
            Vector2 output = Vector2.Zero;
            switch (gyroAction.MotionInput)
            {
                case MotionInput.LocalSpace:
                    output = new Vector2(gyroscope[SENSOR_DEFAULT].Z - gyroscope[SENSOR_DEFAULT].Y, gyroscope[SENSOR_DEFAULT].X);
                    break;
                case MotionInput.PlayerSpace:
                    gamepadMotion.GetPlayerSpaceGyro(out float playerX, out float playerY, 1.41f);
                    output = new Vector2(playerY, playerX);
                    break;
                case MotionInput.WorldSpace:
                    gamepadMotion.GetWorldSpaceGyro(out float worldX, out float worldY, 0.125f);
                    output = new Vector2(worldY, worldX);
                    break;
                case MotionInput.AutoRollYawSwap:
                    gamepadMotion.GetGravity(out float gravityX, out float gravityY, out float gravityZ);
                    output = InputUtils.AutoRollYawSwap(new Vector3(gravityX, gravityY, gravityZ), gyroscope[SENSOR_DEFAULT]);
                    break;
                case MotionInput.JoystickSteering:
                    output.X = InputUtils.Steering(inclination.Angles.Y, currentProfile.SteeringMaxAngle, currentProfile.SteeringPower, currentProfile.SteeringDeadzone);
                    output.Y = 0.0f;
                    break;
            }

            // manage horizontal axis inversion
            if (currentProfile.MotionInvertHorizontal)
                output.X *= -1.0f;

            // manage vertical axis inversion
            if (currentProfile.MotionInvertVertical)
                output.Y *= -1.0f;

            // apply sensivity curve
            // todo: use per-controller sensorSpec ?
            output.X *= InputUtils.ApplyCustomSensitivity(output.X, IMUGyrometer.sensorSpec.maxIn, currentProfile.MotionSensivityArray);
            output.Y *= InputUtils.ApplyCustomSensitivity(output.Y, IMUGyrometer.sensorSpec.maxIn, currentProfile.MotionSensivityArray);

            // apply aiming down scopes multiplier if activated
            if (controllerState.ButtonState.Contains(currentProfile.AimingSightsTrigger))
                output *= currentProfile.AimingSightsMultiplier;

            // apply sensivity
            if (gyroAction.MotionInput != MotionInput.JoystickSteering)
                output = new Vector2(output.X * currentProfile.GetSensitivityX(), output.Y * currentProfile.GetSensitivityY());

            // fill the final calculated state for further use in the remapper
            controllerState.AxisState[AxisFlags.GyroX] = (short)Math.Clamp(output.X, short.MinValue, short.MaxValue);
            controllerState.AxisState[AxisFlags.GyroY] = (short)Math.Clamp(output.Y, short.MinValue, short.MaxValue);
        }
    }
}