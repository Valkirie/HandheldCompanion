using ControllerCommon;
using ControllerCommon.Controllers;
using ControllerCommon.Devices;
using ControllerCommon.Inputs;
using ControllerCommon.Managers;
using ControllerCommon.Utils;
using ControllerService.Sensors;
using Nefarius.ViGEm.Client;
using PrecisionTiming;
using System;
using System.Numerics;
using Platform = ControllerCommon.Platforms.PlatformType;

namespace ControllerService.Targets
{
    public abstract class ViGEmTarget : IDisposable
    {
        public FlickStick flickStick;
        protected ControllerState Inputs = new();
        protected PrecisionTimer UpdateTimer;
        protected const short UPDATE_INTERVAL = 10;

        public HIDmode HID = HIDmode.NoController;

        protected IVirtualGamepad virtualController;

        protected Vector2 LeftThumb;
        protected Vector2 RightThumb;

        public event ConnectedEventHandler Connected;
        public delegate void ConnectedEventHandler(ViGEmTarget target);

        public event DisconnectedEventHandler Disconnected;
        public delegate void DisconnectedEventHandler(ViGEmTarget target);

        public bool IsConnected = false;
        protected bool IsSilenced = false;

        protected ViGEmTarget()
        {
            // initialize flick stick
            flickStick = new FlickStick();

            UpdateTimer = new PrecisionTimer();
            UpdateTimer.SetInterval(UPDATE_INTERVAL);
            UpdateTimer.SetAutoResetMode(true);

            ControllerService.ForegroundUpdated += ForegroundUpdated;
        }

        private void ForegroundUpdated()
        {
            if (ControllerService.currentProfile.Whitelisted)
                IsSilenced = true;
            else
                IsSilenced = false;
        }

        protected void FeedbackReceived(object sender, EventArgs e)
        {
        }

        public override string ToString()
        {
            return EnumUtils.GetDescriptionFromEnumValue(HID);
        }

        public virtual void Connect()
        {
            IsConnected = true;
            Connected?.Invoke(this);
            LogManager.LogInformation("{0} connected", ToString());
        }

        public virtual void Disconnect()
        {
            this.UpdateTimer.Stop();

            IsConnected = false;
            Disconnected?.Invoke(this);
            LogManager.LogInformation("{0} disconnected", ToString());
        }

        public void UpdateInputs(ControllerState inputs)
        {
            Inputs = inputs;
        }

        public virtual unsafe void UpdateReport()
        {
            // get sticks values
            LeftThumb = new Vector2(Inputs.AxisState[AxisFlags.LeftThumbX], Inputs.AxisState[AxisFlags.LeftThumbY]);
            RightThumb = new Vector2(Inputs.AxisState[AxisFlags.RightThumbX], Inputs.AxisState[AxisFlags.RightThumbY]);

            // Improve joystick circularity
            if (ControllerService.currentProfile.thumb_improve_circularity_left)
                LeftThumb = InputUtils.ImproveCircularity(LeftThumb);

            if (ControllerService.currentProfile.thumb_improve_circularity_right)
                RightThumb = InputUtils.ImproveCircularity(RightThumb);

            if (ControllerService.currentProfile.MotionEnabled)
            {
                if ((ControllerService.currentProfile.MotionMode == MotionMode.Off && Inputs.ButtonState.Contains(ControllerService.currentProfile.MotionTrigger)) ||
                    (ControllerService.currentProfile.MotionMode == MotionMode.On && !Inputs.ButtonState.Contains(ControllerService.currentProfile.MotionTrigger)))
                {
                    switch (ControllerService.currentProfile.MotionInput)
                    {
                        case MotionInput.PlayerSpace:
                        case MotionInput.JoystickCamera:
                        case MotionInput.AutoRollYawSwap:
                            {
                                Vector2 Angular;

                                switch (ControllerService.currentProfile.MotionInput)
                                {
                                    case MotionInput.PlayerSpace:
                                        Angular = new Vector2((float)IMU.sensorFusion.CameraYawDelta, (float)IMU.sensorFusion.CameraPitchDelta);
                                        break;
                                    case MotionInput.AutoRollYawSwap:
                                        Angular = InputUtils.AutoRollYawSwap(IMU.sensorFusion.GravityVectorSimple, IMU.AngularVelocity[XInputSensorFlags.Centered]);
                                        break;
                                    default:
                                    case MotionInput.JoystickCamera:
                                        Angular = new Vector2(-IMU.AngularVelocity[XInputSensorFlags.Centered].Z, IMU.AngularVelocity[XInputSensorFlags.Centered].X);
                                        break;
                                }

                                // apply sensivity curve
                                Angular.X *= InputUtils.ApplyCustomSensitivity(Angular.X, IMUGyrometer.sensorSpec.maxIn, ControllerService.currentProfile.MotionSensivityArray);
                                Angular.Y *= InputUtils.ApplyCustomSensitivity(Angular.Y, IMUGyrometer.sensorSpec.maxIn, ControllerService.currentProfile.MotionSensivityArray);

                                // apply aiming down scopes multiplier if activated
                                if (Inputs.ButtonState.Contains(ControllerService.currentProfile.AimingSightsTrigger))
                                {
                                    Angular *= ControllerService.currentProfile.AimingSightsMultiplier;
                                }

                                // apply sensivity
                                Vector2 GamepadThumb = new Vector2(
                                    Angular.X * ControllerService.currentProfile.GetSensitivityX(),
                                    Angular.Y * ControllerService.currentProfile.GetSensitivityY());

                                // apply anti deadzone to motion based thumb input to overcome deadzone and experience small movements properly
                                GamepadThumb = InputUtils.ApplyAntiDeadzone(GamepadThumb, ControllerService.currentProfile.MotionAntiDeadzone);

                                // Improve circularity to prevent 1,1 joystick values based on motion
                                GamepadThumb = InputUtils.ImproveCircularity(GamepadThumb);

                                switch (ControllerService.currentProfile.MotionOutput)
                                {
                                    default:
                                    case MotionOutput.RightStick:

                                        if (ControllerService.currentProfile.FlickstickEnabled)
                                        {
                                            // Flick Stick:
                                            // - Detect flicking
                                            // - Filter stick input
                                            // - Determine and compute either flick or stick output
                                            float FlickStickX = flickStick.Handle(RightThumb,
                                                                                  ControllerService.currentProfile.FlickstickDuration,
                                                                                  ControllerService.currentProfile.FlickstickSensivity,
                                                                                  IMU.TotalMilliseconds);

                                            // X input combines motion controls plus flick stick result
                                            // Y input only from motion controls
                                            RightThumb.X = (short)(Math.Clamp(GamepadThumb.X - FlickStickX, short.MinValue, short.MaxValue));
                                            RightThumb.Y = (short)(Math.Clamp(GamepadThumb.Y, short.MinValue, short.MaxValue));
                                        }
                                        else
                                        {
                                            RightThumb.X = (short)(Math.Clamp(RightThumb.X + GamepadThumb.X, short.MinValue, short.MaxValue));
                                            RightThumb.Y = (short)(Math.Clamp(RightThumb.Y + GamepadThumb.Y, short.MinValue, short.MaxValue));
                                        }
                                        break;

                                    case MotionOutput.LeftStick:
                                        LeftThumb.X = (short)(Math.Clamp(LeftThumb.X + GamepadThumb.X, short.MinValue, short.MaxValue));
                                        LeftThumb.Y = (short)(Math.Clamp(LeftThumb.Y + GamepadThumb.Y, short.MinValue, short.MaxValue));

                                        break;
                                }
                            }
                            break;

                        case MotionInput.JoystickSteering:
                            {
                                float GamepadThumbX = InputUtils.Steering(
                                    IMU.sensorFusion.DeviceAngle.Y,
                                    ControllerService.currentProfile.SteeringMaxAngle,
                                    ControllerService.currentProfile.SteeringPower,
                                    ControllerService.currentProfile.SteeringDeadzone);

                                switch (ControllerService.currentProfile.MotionOutput)
                                {
                                    default:
                                    case MotionOutput.RightStick:
                                        RightThumb.X = (short)GamepadThumbX;
                                        break;
                                    case MotionOutput.LeftStick:
                                        LeftThumb.X = (short)GamepadThumbX;
                                        break;
                                }
                            }
                            break;
                    }
                }
            }
        }

        internal void SubmitReport()
        {
            /*
             * ButtonsInjector = 0;
             * sStateInjector.wButtons = 0;
             */
        }

        public virtual void Dispose()
        {
            this.Disconnect();
            this.UpdateTimer = null;
            GC.SuppressFinalize(this);
        }
    }
}