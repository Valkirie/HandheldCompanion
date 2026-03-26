using HandheldCompanion.Actions;
using HandheldCompanion.Controllers;
using HandheldCompanion.Helpers;
using HandheldCompanion.Inputs;
using HandheldCompanion.Misc;
using HandheldCompanion.Utils;
using HandheldCompanion.Views;
using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using static HandheldCompanion.Utils.DeviceUtils;
using SensorState = HandheldCompanion.Inputs.GyroState.SensorState;

namespace HandheldCompanion.Managers
{
    public static class MotionManager
    {
        private static GyroActions gyroAction = new();
        private static Inclination inclination = new();

        // Accumulates displacement across frames for velocity mode
        // Allows fast movements to spread across multiple frames without losing precision
        private static Vector2 accumulatedDisplacement = Vector2.Zero;

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

        public static void UpdateReport(ControllerState controllerState, GamepadMotion gamepadMotion, float delta = 0.016f)
        {
            SetupMotion(controllerState, gamepadMotion);
            ProcessMotion(controllerState, gamepadMotion, delta);
        }

        // this function sets some basic motion settings, sensitivity and inverts
        // and is enough for DS4/DSU gyroscope handling
        private static void SetupMotion(ControllerState controllerState, GamepadMotion gamepadMotion)
        {
            GyroState gyroState = controllerState.GyroState;

            // GamepadMotion: calibrated/filtered outputs from JoyShockLibrary
            ref Vector3 gyroGM = ref gyroState.GetGyroscopeRef(SensorState.GamepadMotion);
            gamepadMotion.GetCalibratedGyro(out gyroGM.X, out gyroGM.Y, out gyroGM.Z);

            ref Vector3 accelGM = ref gyroState.GetAccelerometerRef(SensorState.GamepadMotion);
            gamepadMotion.GetGravity(out accelGM.X, out accelGM.Y, out accelGM.Z);

            // DSU: unfiltered outputs from sensors
            ref Vector3 gyroDSU = ref gyroState.GetGyroscopeRef(SensorState.DSU);
            gamepadMotion.GetRawGyro(out gyroDSU.X, out gyroDSU.Y, out gyroDSU.Z);

            ref Vector3 accelDSU = ref gyroState.GetAccelerometerRef(SensorState.DSU);
            gamepadMotion.GetRawAcceleration(out accelDSU.X, out accelDSU.Y, out accelDSU.Z);

            // Default: based on GamepadMotion values with profile settings applied
            Profile current = ManagerFactory.profileManager.GetCurrent();

            ref Vector3 defaultGyro = ref gyroState.GetGyroscopeRef(SensorState.Default);
            defaultGyro = gyroState.GetGyroscope(SensorState.GamepadMotion) * current.GyrometerMultiplier;

            ref Vector3 defaultAccel = ref gyroState.GetAccelerometerRef(SensorState.Default);
            defaultAccel = gyroState.GetAccelerometer(SensorState.GamepadMotion) * current.AccelerometerMultiplier;

            // Default: swap roll/yaw/auto
            SteeringAxis steeringAxis = DetermineSteeringAxis(current, controllerState);
            if (steeringAxis == SteeringAxis.Yaw)
            {
                SwapYawRoll(ref defaultGyro);
                SwapYawRoll(ref defaultAccel);
                SwapYawRoll(ref gyroState.GetGyroscopeRef(SensorState.DSU));
                SwapYawRoll(ref gyroState.GetAccelerometerRef(SensorState.DSU));
            }

            // DSU: invert axes if needed
            if (current.MotionInvertHorizontal)
            {
                InvertAxis(ref gyroState.GetGyroscopeRef(SensorState.DSU), Axis.Y, Axis.Z);
                InvertAxis(ref gyroState.GetAccelerometerRef(SensorState.DSU), Axis.Y, Axis.Z);
            }
            if (current.MotionInvertVertical)
            {
                InvertAxis(ref gyroState.GetGyroscopeRef(SensorState.DSU), Axis.X, Axis.Y);
                InvertAxis(ref gyroState.GetAccelerometerRef(SensorState.DSU), Axis.X, Axis.Y);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
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
                if (sensorSelection == SensorFamily.Controller)
                {
                    Vector3 accelerometer = controllerState.GyroState.GetAccelerometer(SensorState.Default);
                    if (MathF.Abs(accelerometer.Z) > MathF.Abs(accelerometer.Y))
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

        private enum Axis { X, Y, Z }

        // this function is used for advanced motion calculations used by
        // gyro to joy/mouse mappings and by UI that configures them
        private static void ProcessMotion(ControllerState controllerState, GamepadMotion gamepadMotion, float delta)
        {
            // TODO: handle this race condition gracefully. LayoutManager might be updating currentlayout as we land here
            Layout currentLayout = ManagerFactory.layoutManager.GetCurrent();
            if (currentLayout is null)
                return;

            ButtonState buttonState = controllerState.ButtonState;
            GyroState gyroState = controllerState.GyroState;
            string currentPageName = MainWindow.CurrentPageName;

            if (currentLayout.GyroLayout.TryGetValue(AxisLayoutFlags.Gyroscope, out IActions action))
                if (action is not null)
                    gyroAction = action as GyroActions;

            MotionMode motionMode = gyroAction.MotionMode;
            MotionInput motionInput = gyroAction.MotionInput;
            bool motionTriggerPressed = buttonState.ContainsTrue(gyroAction.MotionTrigger);

            //toggle motion when trigger is pressed
            if (motionMode == MotionMode.Toggle)
            {
                if (gyroAction.MotionTogglePressed)
                {
                    if (!motionTriggerPressed)
                    {
                        gyroAction.MotionTogglePressed = false; // disable debounce flag
                    }
                }
                else
                {
                    if (motionTriggerPressed)
                    {
                        gyroAction.MotionToggleStatus = !gyroAction.MotionToggleStatus;
                        gyroAction.MotionTogglePressed = true; // enable debounce flag
                    }
                }
            }

            // check if motion input is active
            bool MotionTriggered =
                (motionMode == MotionMode.Off && motionTriggerPressed) ||
                (motionMode == MotionMode.On && !motionTriggerPressed) ||
                (motionMode == MotionMode.Toggle && gyroAction.MotionToggleStatus);

            bool MotionMapped = action?.actionType != ActionType.Disabled;
            Vector3 defaultAccelerometer = gyroState.GetAccelerometer(SensorState.Default);

            // update inclination only when needed
            if ((MotionMapped && MotionTriggered && motionInput == MotionInput.JoystickSteering) || currentPageName == "SettingsMode1")
                inclination.UpdateReport(defaultAccelerometer);

            switch (currentPageName)
            {
                case "SettingsMode0":
                    SettingsMode0Update?.Invoke(gyroState.GetGyroscope(SensorState.Default));
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

                // Reset accumulator when motion is disabled to prevent drift
                accumulatedDisplacement = Vector2.Zero;
                return;
            }

            Profile currentProfile = ManagerFactory.profileManager.GetCurrent();
            Vector2 output = Vector2.Zero;
            switch (motionInput)
            {
                case MotionInput.LocalSpace:
                    Vector3 defaultGyroscope = gyroState.GetGyroscope(SensorState.Default);
                    output = new Vector2(defaultGyroscope.Z, defaultGyroscope.X);
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
            float gyroThreshold = gamepadMotion.GetCalibration().GetGyroThreshold();
            output.X *= InputUtils.ApplyCustomSensitivity(output.X, gyroThreshold, currentProfile.MotionSensivityArray);
            output.Y *= InputUtils.ApplyCustomSensitivity(output.Y, gyroThreshold, currentProfile.MotionSensivityArray);

            // apply aiming down scopes multiplier if activated
            if (controllerState.ButtonState.Contains(currentProfile.AimingSightsTrigger))
                output *= currentProfile.AimingSightsMultiplier;

            // apply velocity-based scaling if enabled
            bool velocityModeEnabled = gyroAction.VelocityMode == GyroVelocityMode.Velocity && motionInput != MotionInput.JoystickSteering;
            if (velocityModeEnabled)
            {
                // Calculate intended displacement from angular velocity
                // Normalize to 60 FPS baseline (delta * 60) so existing sensitivity values still work
                Vector2 intendedDisplacement = output * delta * 60.0f * gyroAction.VelocityScale;

                // Add to accumulator (preserves displacement that couldn't fit in previous frames)
                accumulatedDisplacement += intendedDisplacement;

                // Use accumulated displacement instead of raw output
                output = accumulatedDisplacement;
            }
            else
            {
                // Default mode: reset accumulator to prevent interference
                accumulatedDisplacement = Vector2.Zero;
            }

            // apply sensivity
            float sensitivityX = currentProfile.GetSensitivityX();
            float sensitivityY = currentProfile.GetSensitivityY();
            if (motionInput != MotionInput.JoystickSteering)
            {
                output.X *= sensitivityX;
                output.Y *= sensitivityY;
            }

            // Clamp to output range
            short outputX = (short)Math.Clamp(output.X, short.MinValue, short.MaxValue);
            short outputY = (short)Math.Clamp(output.Y, short.MinValue, short.MaxValue);

            // Consume what we successfully output from the accumulator (before sensitivity was applied)
            if (velocityModeEnabled)
            {
                // Calculate how much raw displacement was consumed (reverse the sensitivity multiplication)
                float consumedX = outputX / sensitivityX;
                float consumedY = outputY / sensitivityY;

                accumulatedDisplacement.X -= consumedX;
                accumulatedDisplacement.Y -= consumedY;

                // Apply decay AFTER consumption to prevent draining useful displacement
                // This naturally dissipates small residual values while preserving large flicks
                const float decayFactor = 0.90f; // 10% decay per frame
                accumulatedDisplacement *= decayFactor;
            }

            // fill the final calculated state for further use in the remapper
            controllerState.AxisState[AxisFlags.GyroX] = outputX;
            controllerState.AxisState[AxisFlags.GyroY] = outputY;
        }
    }
}