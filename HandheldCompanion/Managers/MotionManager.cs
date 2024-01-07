using HandheldCompanion.Actions;
using HandheldCompanion.Controllers;
using HandheldCompanion.Inputs;
using HandheldCompanion.Misc;
using HandheldCompanion.Sensors;
using HandheldCompanion.Utils;
using HandheldCompanion.Views;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Windows;

namespace HandheldCompanion.Managers
{
    public static class MotionManager
    {
        [Flags]
        private enum SensorIndex
        {
            Raw = 0,
            Default = 1,
        }

        private static Vector3[] accelerometer = new Vector3[2];
        private static Vector3[] gyroscope = new Vector3[2];
        private static GyroActions gyroAction = new();

        private static SensorFusion sensorFusion = new();
        private static MadgwickAHRS madgwickAHRS;
        private static Inclination inclination = new();

        private static double PreviousTotalMilliseconds;
        public static double DeltaSeconds;

        private static IEnumerable<ButtonFlags> resetFlags = new List<ButtonFlags>() { ButtonFlags.B1, ButtonFlags.B2, ButtonFlags.B3, ButtonFlags.B4 };

        public static event SettingsMode0EventHandler SettingsMode0Update;
        public delegate void SettingsMode0EventHandler(Vector3 gyrometer);

        public static event SettingsMode1EventHandler SettingsMode1Update;
        public delegate void SettingsMode1EventHandler(Vector2 deviceAngle);

        public static event OverlayModelEventHandler OverlayModelUpdate;
        public delegate void OverlayModelEventHandler(Vector3 euler, Quaternion quaternion);

        public static bool IsInitialized;

        public static event InitializedEventHandler Initialized;
        public delegate void InitializedEventHandler();

        static MotionManager()
        {
            float samplePeriod = TimerManager.GetPeriod() / 1000f;
            madgwickAHRS = new(samplePeriod, 0.01f);
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

        public static void UpdateReport(ControllerState controllerState)
        {
            SetupMotion(controllerState);
            CalculateMotion(controllerState);
            
            if (controllerState.ButtonState.Buttons.Intersect(resetFlags).Count() == 4)
                madgwickAHRS.Reset();
        }

        // this function sets some basic motion settings, sensitivity and inverts
        // and is enough for DS4/DSU gyroscope handling
        private static void SetupMotion(ControllerState controllerState)
        {
            // store raw values for later use
            accelerometer[(int)SensorIndex.Raw] = controllerState.GyroState.Accelerometer;
            gyroscope[(int)SensorIndex.Raw] = controllerState.GyroState.Gyroscope;

            Profile current = ProfileManager.GetCurrent();

            accelerometer[(int)SensorIndex.Default].Z = current.SteeringAxis == 0 ? controllerState.GyroState.Accelerometer.Z : controllerState.GyroState.Accelerometer.Y;
            accelerometer[(int)SensorIndex.Default].Y = current.SteeringAxis == 0 ? controllerState.GyroState.Accelerometer.Y : -controllerState.GyroState.Accelerometer.Z;
            accelerometer[(int)SensorIndex.Default].X = current.SteeringAxis == 0 ? controllerState.GyroState.Accelerometer.X : controllerState.GyroState.Accelerometer.X;
            accelerometer[(int)SensorIndex.Default] *= current.AccelerometerMultiplier;

            gyroscope[(int)SensorIndex.Default].Z = current.SteeringAxis == 0 ? controllerState.GyroState.Gyroscope.Z : controllerState.GyroState.Gyroscope.Y;
            gyroscope[(int)SensorIndex.Default].Y = current.SteeringAxis == 0 ? controllerState.GyroState.Gyroscope.Y : controllerState.GyroState.Gyroscope.Z;
            gyroscope[(int)SensorIndex.Default].X = current.SteeringAxis == 0 ? controllerState.GyroState.Gyroscope.X : controllerState.GyroState.Gyroscope.X;
            gyroscope[(int)SensorIndex.Default] *= current.GyrometerMultiplier;

            if (current.MotionInvertHorizontal)
            {
                accelerometer[(int)SensorIndex.Default].Y *= -1.0f;
                accelerometer[(int)SensorIndex.Default].Z *= -1.0f;
                gyroscope[(int)SensorIndex.Default].Y *= -1.0f;
                gyroscope[(int)SensorIndex.Default].Z *= -1.0f;
            }

            if (current.MotionInvertVertical)
            {
                accelerometer[(int)SensorIndex.Default].Y *= -1.0f;
                accelerometer[(int)SensorIndex.Default].X *= -1.0f;
                gyroscope[(int)SensorIndex.Default].Y *= -1.0f;
                gyroscope[(int)SensorIndex.Default].X *= -1.0f;
            }

            // store modified values, they are used by DS4 and DSU, raws are only used later on in MotionManager
            controllerState.GyroState.Accelerometer = accelerometer[(int)SensorIndex.Default];
            controllerState.GyroState.Gyroscope = gyroscope[(int)SensorIndex.Default];
        }

        // this function is used for advanced motion calculations used by
        // gyro to joy/mouse mappings, by UI that configures them and by 3D overlay
        private static void CalculateMotion(ControllerState controllerState)
        {
            Profile current = ProfileManager.GetCurrent();

            if (MainWindow.overlayModel.Visibility == Visibility.Visible && MainWindow.overlayModel.MotionActivated)
            {
                Vector3 AngularVelocityRad = new(
                    -InputUtils.deg2rad(gyroscope[(int)SensorIndex.Raw].X),
                    -InputUtils.deg2rad(gyroscope[(int)SensorIndex.Raw].Y),
                    -InputUtils.deg2rad(gyroscope[(int)SensorIndex.Raw].Z));
                madgwickAHRS.UpdateReport(
                    AngularVelocityRad.X,
                    AngularVelocityRad.Y,
                    AngularVelocityRad.Z,
                    -accelerometer[(int)SensorIndex.Raw].X,
                    accelerometer[(int)SensorIndex.Raw].Y,
                    accelerometer[(int)SensorIndex.Raw].Z,
                    DeltaSeconds);

                OverlayModelUpdate?.Invoke(madgwickAHRS.GetEuler(), madgwickAHRS.GetQuaternion());
            }

            if (current.Layout.GyroLayout.TryGetValue(AxisLayoutFlags.Gyroscope, out IActions action))
                if (action is not null)
                    gyroAction = action as GyroActions;

            // update timestamp
            double TotalMilliseconds = TimerManager.Stopwatch.Elapsed.TotalMilliseconds;
            DeltaSeconds = (TotalMilliseconds - PreviousTotalMilliseconds) / 1000;
            PreviousTotalMilliseconds = TotalMilliseconds;

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

            // update sensorFusion, only when needed
            if (MotionMapped && MotionTriggered && (gyroAction.MotionInput == MotionInput.PlayerSpace ||
                                                    gyroAction.MotionInput == MotionInput.AutoRollYawSwap))
            {
                sensorFusion.UpdateReport(DeltaSeconds, gyroscope[(int)SensorIndex.Default], accelerometer[(int)SensorIndex.Default]);
            }

            if ((MotionMapped && MotionTriggered && gyroAction.MotionInput == MotionInput.JoystickSteering)
                || MainWindow.CurrentPageName == "SettingsMode1")
            {
                inclination.UpdateReport(accelerometer[(int)SensorIndex.Default]);
            }

            switch (MainWindow.CurrentPageName)
            {
                case "SettingsMode0":
                    SettingsMode0Update?.Invoke(gyroscope[(int)SensorIndex.Default]);
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

            Vector2 output;

            switch (gyroAction.MotionInput)
            {
                case MotionInput.PlayerSpace:
                case MotionInput.AutoRollYawSwap:
                case MotionInput.JoystickCamera:
                default:
                    switch (gyroAction.MotionInput)
                    {
                        case MotionInput.PlayerSpace:
                            output = new Vector2((float)sensorFusion.CameraYawDelta, (float)sensorFusion.CameraPitchDelta);
                            break;
                        case MotionInput.AutoRollYawSwap:
                            output = InputUtils.AutoRollYawSwap(sensorFusion.GravityVectorSimple, gyroscope[(int)SensorIndex.Default]);
                            break;
                        case MotionInput.JoystickCamera:
                        default:
                            output = new Vector2(gyroscope[(int)SensorIndex.Default].Z, gyroscope[(int)SensorIndex.Default].X);
                            break;
                    }

                    // apply sensivity curve
                    // todo: use per-controller sensorSpec ?
                    output.X *= InputUtils.ApplyCustomSensitivity(output.X, IMUGyrometer.sensorSpec.maxIn, current.MotionSensivityArray);
                    output.Y *= InputUtils.ApplyCustomSensitivity(output.Y, IMUGyrometer.sensorSpec.maxIn, current.MotionSensivityArray);

                    // apply aiming down scopes multiplier if activated
                    if (controllerState.ButtonState.Contains(current.AimingSightsTrigger))
                        output *= current.AimingSightsMultiplier;

                    // apply sensivity
                    output = new Vector2(output.X * current.GetSensitivityX(), output.Y * current.GetSensitivityY());

                    break;

                // TODO: merge this somehow with joy Y as it was previously?
                case MotionInput.JoystickSteering:
                    {
                        output.X = InputUtils.Steering(inclination.Angles.Y, current.SteeringMaxAngle, current.SteeringPower, current.SteeringDeadzone);
                        output.Y = 0.0f;
                    }
                    break;
            }

            // fill the final calculated state for further use in the remapper
            controllerState.AxisState[AxisFlags.GyroX] = (short)Math.Clamp(output.X, short.MinValue, short.MaxValue);
            controllerState.AxisState[AxisFlags.GyroY] = (short)Math.Clamp(output.Y, short.MinValue, short.MaxValue);
        }
    }
}