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
using System.Runtime.CompilerServices;
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
            {
                var gyroGM = controllerState.GyroState.GetGyroscope(GyroState.SensorState.GamepadMotion);
                gamepadMotion.GetCalibratedGyro(out gyroGM.X, out gyroGM.Y, out gyroGM.Z);

                var accelGM = controllerState.GyroState.GetAccelerometer(GyroState.SensorState.GamepadMotion);
                gamepadMotion.GetGravity(out accelGM.X, out accelGM.Y, out accelGM.Z);
            }

            // DSU: unfiltered outputs from sensors
            {
                var gyroDSU = controllerState.GyroState.GetGyroscope(GyroState.SensorState.DSU);
                gamepadMotion.GetRawGyro(out gyroDSU.X, out gyroDSU.Y, out gyroDSU.Z);

                var accelDSU = controllerState.GyroState.GetAccelerometer(GyroState.SensorState.DSU);
                gamepadMotion.GetRawAcceleration(out accelDSU.X, out accelDSU.Y, out accelDSU.Z);
            }

            // Default: based on GamepadMotion values with profile settings applied
            Profile current = ManagerFactory.profileManager.GetCurrent();

            controllerState.GyroState.SetGyroscope(
                GyroState.SensorState.Default,
                controllerState.GyroState.GetGyroscope(GyroState.SensorState.GamepadMotion) * current.GyrometerMultiplier);

            controllerState.GyroState.SetAccelerometer(
                GyroState.SensorState.Default,
                controllerState.GyroState.GetAccelerometer(GyroState.SensorState.GamepadMotion) * current.AccelerometerMultiplier);

            // Default: swap roll/yaw/auto
            SteeringAxis steeringAxis = DetermineSteeringAxis(current, controllerState);
            if (steeringAxis == SteeringAxis.Yaw)
            {
                SwapYawRoll(ref controllerState.GyroState.GetGyroscopeRef(GyroState.SensorState.Default));
                SwapYawRoll(ref controllerState.GyroState.GetAccelerometerRef(GyroState.SensorState.Default));
                SwapYawRoll(ref controllerState.GyroState.GetGyroscopeRef(GyroState.SensorState.DSU));
                SwapYawRoll(ref controllerState.GyroState.GetAccelerometerRef(GyroState.SensorState.DSU));
            }

            // DSU: invert axes if needed
            if (current.MotionInvertHorizontal)
            {
                InvertAxis(ref controllerState.GyroState.GetGyroscopeRef(GyroState.SensorState.DSU), Axis.Y, Axis.Z);
                InvertAxis(ref controllerState.GyroState.GetAccelerometerRef(GyroState.SensorState.DSU), Axis.Y, Axis.Z);
            }
            if (current.MotionInvertVertical)
            {
                InvertAxis(ref controllerState.GyroState.GetGyroscopeRef(GyroState.SensorState.DSU), Axis.X, Axis.Y);
                InvertAxis(ref controllerState.GyroState.GetAccelerometerRef(GyroState.SensorState.DSU), Axis.X, Axis.Y);
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
                    Math.Abs(controllerState.GyroState.GetAccelerometer(SensorState.Default).Z) > Math.Abs(controllerState.GyroState.GetAccelerometer(SensorState.Default).Y))
                {
                    return SteeringAxis.Yaw;
                }
            }
            return steeringAxis;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void SwapYawRoll(ref Vector3 v)
        {
            v = new Vector3(v.X, -v.Z, -v.Y);
        }

        // Convenience wrappers for each source in GyroState
        private static void SwapYawRollGyro(GyroState gyro, GyroState.SensorState state)
        {
            ref var v = ref gyro.GetGyroscopeRef(state);
            SwapYawRoll(ref v);
        }

        private static void SwapYawRollAccel(GyroState gyro, GyroState.SensorState state)
        {
            ref var v = ref gyro.GetAccelerometerRef(state);
            SwapYawRoll(ref v);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void InvertAxis(ref Vector3 v, Axis axis1, Axis axis2)
        {
            InvertOne(ref v, axis1);
            InvertOne(ref v, axis2);

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            static void InvertOne(ref Vector3 vec, Axis a)
            {
                switch (a)
                {
                    case Axis.X: vec.X = -vec.X; break;
                    case Axis.Y: vec.Y = -vec.Y; break;
                    case Axis.Z: vec.Z = -vec.Z; break;
                }
            }
        }

        private static void InvertAxisGyro(GyroState gyro, GyroState.SensorState state, Axis a1, Axis a2)
        {
            ref var v = ref gyro.GetGyroscopeRef(state);
            InvertAxis(ref v, a1, a2);
        }

        private static void InvertAxisAccel(GyroState gyro, GyroState.SensorState state, Axis a1, Axis a2)
        {
            ref var v = ref gyro.GetAccelerometerRef(state);
            InvertAxis(ref v, a1, a2);
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
                inclination.UpdateReport(controllerState.GyroState.GetAccelerometer(SensorState.Default));

            switch (MainWindow.CurrentPageName)
            {
                case "SettingsMode0":
                    SettingsMode0Update?.Invoke(controllerState.GyroState.GetGyroscope(SensorState.Default));
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
                    output = new Vector2(controllerState.GyroState.GetGyroscope(SensorState.Default).Z - controllerState.GyroState.GetGyroscope(SensorState.Default).Y, controllerState.GyroState.GetGyroscope(SensorState.Default).X);
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