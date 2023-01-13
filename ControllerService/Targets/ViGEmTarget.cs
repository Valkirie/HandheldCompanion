using ControllerCommon;
using ControllerCommon.Controllers;
using ControllerCommon.Devices;
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
        protected ControllerInputs Inputs = new();
        protected PrecisionTimer UpdateTimer;

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
            UpdateTimer.SetInterval(5);
            UpdateTimer.SetAutoResetMode(true);

            ControllerService.ForegroundUpdated += ForegroundUpdated;
        }

        private void ForegroundUpdated()
        {
            if (ControllerService.currentProfile.whitelisted)
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

        public void UpdateInputs(ControllerInputs inputs)
        {
            Inputs = inputs;
        }

        public virtual unsafe void UpdateReport()
        {
            // get sticks values
            LeftThumb = new Vector2(Inputs.LeftThumbX, Inputs.LeftThumbY);
            RightThumb = new Vector2(Inputs.RightThumbX, Inputs.RightThumbY);

            // Apply user defined in game deadzone setting compensation prior to UMC additions
            LeftThumb = InputUtils.ApplyAntiDeadzone(LeftThumb, ControllerService.currentProfile.antideadzoneL);
            RightThumb = InputUtils.ApplyAntiDeadzone(RightThumb, ControllerService.currentProfile.antideadzoneR);

            if (ControllerService.currentProfile.umc_enabled)
            {
                if ((ControllerService.currentProfile.umc_motion_defaultoffon == UMC_Motion_Default.Off && (ControllerService.currentProfile.umc_trigger & Inputs.Buttons) != 0) ||
                    (ControllerService.currentProfile.umc_motion_defaultoffon == UMC_Motion_Default.On && (ControllerService.currentProfile.umc_trigger & Inputs.Buttons) == 0))
                {
                    switch (ControllerService.currentProfile.umc_input)
                    {
                        case Input.PlayerSpace:
                        case Input.JoystickCamera:
                        case Input.AutoRollYawSwap:
                            {
                                Vector2 Angular;

                                switch (ControllerService.currentProfile.umc_input)
                                {
                                    case Input.PlayerSpace:
                                        Angular = new Vector2((float)IMU.sensorFusion.CameraYawDelta, (float)IMU.sensorFusion.CameraPitchDelta);
                                        break;
                                    case Input.AutoRollYawSwap:
                                        Angular = InputUtils.AutoRollYawSwap(IMU.sensorFusion.GravityVectorSimple, IMU.AngularVelocity[XInputSensorFlags.Centered]);
                                        break;
                                    default:
                                    case Input.JoystickCamera:
                                        Angular = new Vector2(-IMU.AngularVelocity[XInputSensorFlags.Centered].Z, IMU.AngularVelocity[XInputSensorFlags.Centered].X);
                                        break;
                                }

                                // apply sensivity curve
                                Angular.X *= InputUtils.ApplyCustomSensitivity(Angular.X, IMUGyrometer.sensorSpec.maxIn, ControllerService.currentProfile.aiming_array);
                                Angular.Y *= InputUtils.ApplyCustomSensitivity(Angular.Y, IMUGyrometer.sensorSpec.maxIn, ControllerService.currentProfile.aiming_array);

                                // apply aiming down scopes multiplier if activated
                                if ((ControllerService.currentProfile.aiming_down_sights_activation & Inputs.Buttons) != 0)
                                {
                                    Angular *= ControllerService.currentProfile.aiming_down_sights_multiplier;
                                }

                                // apply sensivity
                                Vector2 GamepadThumb = new Vector2(
                                    Angular.X * ControllerService.currentProfile.GetSensitivityX(),
                                    Angular.Y * ControllerService.currentProfile.GetSensitivityY());

                                // apply anti deadzone to motion based thumb input to overcome deadzone and experience small movements properly
                                GamepadThumb = InputUtils.ApplyAntiDeadzone(GamepadThumb, ControllerService.currentProfile.umc_anti_deadzone);

                                switch (ControllerService.currentProfile.umc_output)
                                {
                                    default:
                                    case Output.RightStick:

                                        if (ControllerService.currentProfile.flickstick_enabled)
                                        {
                                            // Flick Stick:
                                            // - Detect flicking
                                            // - Filter stick input
                                            // - Determine and compute either flick or stick output
                                            float FlickStickX = flickStick.Handle(RightThumb,
                                                                                  ControllerService.currentProfile.flick_duration,
                                                                                  ControllerService.currentProfile.stick_sensivity,
                                                                                  IMU.TotalMilliseconds);

                                            // X input combines motion controls plus flick stick result
                                            // Y input only from motion controls
                                            RightThumb.X = (short)(Math.Clamp(GamepadThumb.X - FlickStickX, short.MinValue, short.MaxValue));
                                            RightThumb.Y = (short)(Math.Clamp(GamepadThumb.Y, short.MinValue, short.MaxValue));

                                            RightThumb = InputUtils.ImproveCircularity(RightThumb);
                                        }
                                        else
                                        {
                                            RightThumb.X = (short)(Math.Clamp(RightThumb.X + GamepadThumb.X, short.MinValue, short.MaxValue));
                                            RightThumb.Y = (short)(Math.Clamp(RightThumb.Y + GamepadThumb.Y, short.MinValue, short.MaxValue));

                                            RightThumb = InputUtils.ImproveCircularity(RightThumb);
                                        }
                                        break;

                                    case Output.LeftStick:
                                        LeftThumb.X = (short)(Math.Clamp(LeftThumb.X + GamepadThumb.X, short.MinValue, short.MaxValue));
                                        LeftThumb.Y = (short)(Math.Clamp(LeftThumb.Y + GamepadThumb.Y, short.MinValue, short.MaxValue));

                                        LeftThumb = InputUtils.ImproveCircularity(LeftThumb);
                                        break;
                                }
                            }
                            break;

                        case Input.JoystickSteering:
                            {
                                float GamepadThumbX = InputUtils.Steering(
                                    IMU.sensorFusion.DeviceAngle.Y,
                                    ControllerService.currentProfile.steering_max_angle,
                                    ControllerService.currentProfile.steering_power,
                                    ControllerService.currentProfile.steering_deadzone);

                                switch (ControllerService.currentProfile.umc_output)
                                {
                                    default:
                                    case Output.RightStick:
                                        RightThumb.X = (short)GamepadThumbX;
                                        break;
                                    case Output.LeftStick:
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