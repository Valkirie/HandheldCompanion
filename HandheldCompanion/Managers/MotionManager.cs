using HandheldCompanion.Actions;
using HandheldCompanion.Controllers;
using HandheldCompanion.Helpers;
using HandheldCompanion.Inputs;
using HandheldCompanion.Misc;
using HandheldCompanion.Utils;
using HandheldCompanion.Views;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.InteropServices;
using static HandheldCompanion.Utils.DeviceUtils;
using SensorState = HandheldCompanion.Inputs.GyroState.SensorState;

namespace HandheldCompanion.Managers
{
    public static class MotionManager
    {
        private static GyroActions gyroAction = new();
        private static Inclination inclination = new();

        public static event SettingsMode0EventHandler SettingsMode0Update;
        public delegate void SettingsMode0EventHandler(Vector3 gyrometer);

        public static event SettingsMode1EventHandler SettingsMode1Update;
        public delegate void SettingsMode1EventHandler(Vector2 deviceAngle);

        public static bool IsInitialized;

        public static event InitializedEventHandler Initialized;
        public delegate void InitializedEventHandler();

        static MotionManager()
        {
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

        public static void UpdateReport(ControllerState controllerState, GamepadMotion gamepadMotion)
        {
            SetupMotion(controllerState, gamepadMotion);
            ProcessMotion(controllerState, gamepadMotion);
        }

        private static ref Vector3 GetGyroRef(Dictionary<SensorState, Vector3> dictionary, SensorState state)
        {
            return ref CollectionsMarshal.GetValueRefOrNullRef(dictionary, state);
        }

        // this function sets some basic motion settings, sensitivity and inverts
        // and is enough for DS4/DSU gyroscope handling
        private static void SetupMotion(ControllerState controllerState, GamepadMotion gamepadMotion)
        {
            // GamepadMotion: calibrated/filtered outputs from JoyShockLibrary
            if (controllerState.GyroState.Gyroscope.ContainsKey(SensorState.GamepadMotion))
            {
                ref Vector3 gyrometer = ref GetGyroRef(controllerState.GyroState.Gyroscope, SensorState.GamepadMotion);
                gamepadMotion.GetCalibratedGyro(out gyrometer.X, out gyrometer.Y, out gyrometer.Z);
            }
            if (controllerState.GyroState.Accelerometer.ContainsKey(SensorState.GamepadMotion))
            {
                ref Vector3 accelerometer = ref GetGyroRef(controllerState.GyroState.Accelerometer, SensorState.GamepadMotion);
                gamepadMotion.GetGravity(out accelerometer.X, out accelerometer.Y, out accelerometer.Z);
            }

            // DSU: unfiltered outputs from sensors
            if (controllerState.GyroState.Gyroscope.ContainsKey(SensorState.DSU))
            {
                ref Vector3 gyrometer = ref GetGyroRef(controllerState.GyroState.Gyroscope, SensorState.DSU);
                gamepadMotion.GetRawGyro(out gyrometer.X, out gyrometer.Y, out gyrometer.Z);
            }
            if (controllerState.GyroState.Accelerometer.ContainsKey(SensorState.DSU))
            {
                ref Vector3 accelerometer = ref GetGyroRef(controllerState.GyroState.Accelerometer, SensorState.DSU);
                gamepadMotion.GetRawAcceleration(out accelerometer.X, out accelerometer.Y, out accelerometer.Z);
            }

            // Default: based on GamepadMotionHelpers values with profile settings applied
            Profile current = ManagerFactory.profileManager.GetCurrent();

            controllerState.GyroState.Gyroscope[SensorState.Default] = controllerState.GyroState.Gyroscope[SensorState.GamepadMotion] * current.GyrometerMultiplier;
            controllerState.GyroState.Accelerometer[SensorState.Default] = controllerState.GyroState.Accelerometer[SensorState.GamepadMotion] * current.AccelerometerMultiplier;

            // Default: swap roll/yaw/auto
            SteeringAxis steeringAxis = DetermineSteeringAxis(current, controllerState);
            if (steeringAxis == SteeringAxis.Yaw)
            {
                SwapYawRoll(controllerState.GyroState.Gyroscope, SensorState.Default);
                SwapYawRoll(controllerState.GyroState.Accelerometer, SensorState.Default);
                SwapYawRoll(controllerState.GyroState.Gyroscope, SensorState.DSU);
                SwapYawRoll(controllerState.GyroState.Accelerometer, SensorState.DSU);
            }

            // DSU: invert axes if needed
            if (current.MotionInvertHorizontal)
            {
                InvertAxis(controllerState.GyroState.Gyroscope, SensorState.DSU, Axis.Y, Axis.Z);
                InvertAxis(controllerState.GyroState.Accelerometer, SensorState.DSU, Axis.Y, Axis.Z);
            }
            if (current.MotionInvertVertical)
            {
                InvertAxis(controllerState.GyroState.Gyroscope, SensorState.DSU, Axis.X, Axis.Y);
                InvertAxis(controllerState.GyroState.Accelerometer, SensorState.DSU, Axis.X, Axis.Y);
            }
        }

        private static SteeringAxis DetermineSteeringAxis(Profile current, ControllerState controllerState)
        {
            SteeringAxis steeringAxis = current.SteeringAxis;
            if (steeringAxis == SteeringAxis.Auto)
            {
                SensorFamily sensorSelection = (SensorFamily)ManagerFactory.settingsManager.GetInt("SensorSelection");
                if (sensorSelection == SensorFamily.Windows || sensorSelection == SensorFamily.SerialUSBIMU)
                {
                    return SteeringAxis.Yaw;
                }
                if (sensorSelection == SensorFamily.Controller &&
                    Math.Abs(controllerState.GyroState.Accelerometer[SensorState.Default].Z) > Math.Abs(controllerState.GyroState.Accelerometer[SensorState.Default].Y))
                {
                    return SteeringAxis.Yaw;
                }
            }
            return steeringAxis;
        }

        private static void SwapYawRoll(Dictionary<SensorState, Vector3> sensorDictionary, SensorState state)
        {
            if (sensorDictionary.TryGetValue(state, out Vector3 sensor))
            {
                sensor = new Vector3(sensor.X, -sensor.Z, -sensor.Y);
                sensorDictionary[state] = sensor;
            }
        }

        private static void InvertAxis(Dictionary<SensorState, Vector3> sensorDictionary, SensorState state, Axis axis1, Axis axis2)
        {
            if (sensorDictionary.TryGetValue(state, out Vector3 sensor))
            {
                switch (axis1)
                {
                    case Axis.X: sensor.X = -sensor.X; break;
                    case Axis.Y: sensor.Y = -sensor.Y; break;
                    case Axis.Z: sensor.Z = -sensor.Z; break;
                }
                switch (axis2)
                {
                    case Axis.X: sensor.X = -sensor.X; break;
                    case Axis.Y: sensor.Y = -sensor.Y; break;
                    case Axis.Z: sensor.Z = -sensor.Z; break;
                }
                sensorDictionary[state] = sensor;
            }
        }

        private enum Axis { X, Y, Z }

        // this function is used for advanced motion calculations used by
        // gyro to joy/mouse mappings and by UI that configures them
        private static void ProcessMotion(ControllerState controllerState, GamepadMotion gamepadMotion)
        {
            // TODO: handle this race condition gracefully. LayoutManager might be updating currentlayout as we land here
            Layout currentLayout = ManagerFactory.layoutManager.GetCurrent();
            if (currentLayout is null)
                return;

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
                inclination.UpdateReport(controllerState.GyroState.Accelerometer[SensorState.Default]);

            switch (MainWindow.CurrentPageName)
            {
                case "SettingsMode0":
                    SettingsMode0Update?.Invoke(controllerState.GyroState.Gyroscope[SensorState.Default]);
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

            Profile currentProfile = ManagerFactory.profileManager.GetCurrent();
            Vector2 output = Vector2.Zero;
            switch (gyroAction.MotionInput)
            {
                case MotionInput.LocalSpace:
                    output = new Vector2(controllerState.GyroState.Gyroscope[SensorState.Default].Z - controllerState.GyroState.Gyroscope[SensorState.Default].Y, controllerState.GyroState.Gyroscope[SensorState.Default].X);
                    break;
                case MotionInput.PlayerSpace:
                    gamepadMotion.GetPlayerSpaceGyro(out float playerX, out float playerY, 1.41f);
                    output = new Vector2(-playerY, playerX);
                    break;
                case MotionInput.WorldSpace:
                    gamepadMotion.GetWorldSpaceGyro(out float worldX, out float worldY, 0.125f);
                    output = new Vector2(-worldY, worldX);
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
            // todo: we should only apply this to gyro based output, maybe only to local space ?
            output.X *= InputUtils.ApplyCustomSensitivity(output.X, gamepadMotion.GetCalibration().GetGyroThreshold(), currentProfile.MotionSensivityArray);
            output.Y *= InputUtils.ApplyCustomSensitivity(output.Y, gamepadMotion.GetCalibration().GetGyroThreshold(), currentProfile.MotionSensivityArray);

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