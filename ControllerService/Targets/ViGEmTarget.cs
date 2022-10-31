using ControllerCommon;
using ControllerCommon.Managers;
using ControllerCommon.Utils;
using ControllerService.Sensors;
using Nefarius.ViGEm.Client;
using SharpDX.XInput;
using System;
using System.Numerics;
using System.Runtime.InteropServices;
using GamepadButtonFlagsExt = ControllerCommon.Utils.GamepadButtonFlagsExt;

namespace ControllerService.Targets
{
    public abstract class ViGEmTarget : IDisposable
    {
        #region imports
        [StructLayout(LayoutKind.Sequential)]
        protected struct XInputStateSecret
        {
            public uint eventCount;
            public ushort wButtons;
            public byte bLeftTrigger;
            public byte bRightTrigger;
            public short sThumbLX;
            public short sThumbLY;
            public short sThumbRX;
            public short sThumbRY;
        }

        [DllImport("xinput1_3.dll", EntryPoint = "#100")]
        protected static extern int XInputGetStateSecret13(int playerIndex, out XInputStateSecret struc);
        [DllImport("xinput1_4.dll", EntryPoint = "#100")]
        protected static extern int XInputGetStateSecret14(int playerIndex, out XInputStateSecret struc);
        #endregion

        public Controller physicalController;
        public XInputController xinputController;
        public FlickStick flickStick;

        public HIDmode HID = HIDmode.NoController;

        protected ViGEmClient client { get; }
        protected IVirtualGamepad virtualController;

        protected XInputStateSecret sState;
        protected XInputStateSecret sStateInjector;

        protected GamepadButtonFlagsExt Buttons;            // used to read/write buttons
        protected GamepadButtonFlagsExt ButtonsInjector;    // used to store injected buttons

        protected double vibrationStrength;

        protected Vector2 LeftThumb;
        protected Vector2 RightThumb;

        public event ConnectedEventHandler Connected;
        public delegate void ConnectedEventHandler(ViGEmTarget target);

        public event DisconnectedEventHandler Disconnected;
        public delegate void DisconnectedEventHandler(ViGEmTarget target);

        public bool IsConnected = false;

        protected ViGEmTarget(XInputController xinput, ViGEmClient client)
        {
            // initialize flick stick
            flickStick = new FlickStick();

            // initialize secret state
            sState = new();

            // initialize controller
            this.client = client;
            this.xinputController = xinput;
            this.physicalController = xinput.controllerEx.Controller;
        }

        protected void FeedbackReceived(object sender, EventArgs e)
        {
        }

        public void SetVibrationStrength(double strength)
        {
            vibrationStrength = strength / 100.0f;
            LogManager.LogInformation("{0} vibration strength set to {1}%", ToString(), strength);
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
            IsConnected = false;
            Disconnected?.Invoke(this);
            LogManager.LogInformation("{0} disconnected", ToString());
        }

        public void InjectReport(GamepadButtonFlagsExt buttons, ushort sButtons)
        {
            ButtonsInjector |= Buttons;
            sStateInjector.wButtons |= sButtons;
        }

        public virtual unsafe void UpdateReport(Gamepad Gamepad)
        {
            // get current gamepad state
            XInputGetStateSecret13((int)physicalController.UserIndex, out sState);
            sState.wButtons |= sStateInjector.wButtons;

            // get buttons values
            Buttons = (GamepadButtonFlagsExt)Gamepad.Buttons;
            Buttons |= (Gamepad.LeftTrigger > 0 ? GamepadButtonFlagsExt.LeftTrigger : 0);
            Buttons |= (Gamepad.RightTrigger > 0 ? GamepadButtonFlagsExt.RightTrigger : 0);
            Buttons |= ButtonsInjector;

            // get sticks values
            LeftThumb = new Vector2(Gamepad.LeftThumbX, Gamepad.LeftThumbY);
            RightThumb = new Vector2(Gamepad.RightThumbX, Gamepad.RightThumbY);

            if (ControllerService.currentProfile.umc_enabled)
            {
                if (((ControllerService.currentProfile.umc_motion_defaultoffon == UMC_Motion_Default.Off) && (ControllerService.currentProfile.umc_trigger & Buttons) != 0) ||
                    ((ControllerService.currentProfile.umc_motion_defaultoffon == UMC_Motion_Default.On) && (ControllerService.currentProfile.umc_trigger & Buttons) == 0))
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
                                        Angular = new Vector2((float)xinputController.sensorFusion.CameraYawDelta, (float)xinputController.sensorFusion.CameraPitchDelta);
                                        break;
                                    case Input.AutoRollYawSwap:
                                        Angular = InputUtils.AutoRollYawSwap(xinputController.sensorFusion.GravityVectorSimple, xinputController.AngularVelocities[XInputSensorFlags.Centered]);
                                        break;
                                    default:
                                    case Input.JoystickCamera:
                                        Angular = new Vector2(-xinputController.AngularVelocities[XInputSensorFlags.Centered].Z, xinputController.AngularVelocities[XInputSensorFlags.Centered].X);
                                        break;
                                }

                                // apply sensivity curve
                                Angular.X *= InputUtils.ApplyCustomSensitivity(Angular.X, XInputGirometer.sensorSpec.maxIn, ControllerService.currentProfile.aiming_array);
                                Angular.Y *= InputUtils.ApplyCustomSensitivity(Angular.Y, XInputGirometer.sensorSpec.maxIn, ControllerService.currentProfile.aiming_array);

                                // apply device width ratio
                                Angular.X *= ControllerService.handheldDevice.WidthHeightRatio;

                                // apply aiming down scopes multiplier if activated
                                if ((ControllerService.currentProfile.aiming_down_sights_activation & Buttons) != 0)
                                {
                                    Angular *= ControllerService.currentProfile.aiming_down_sights_multiplier;
                                }

                                // apply sensivity
                                Vector2 GamepadThumb = new Vector2(
                                    Angular.X * ControllerService.currentProfile.GetSensiviy(),
                                    Angular.Y * ControllerService.currentProfile.GetSensiviy());

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
                                                                                  XInputController.TotalMilliseconds);

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

                                    case Output.LeftStick:
                                        LeftThumb.X = (short)(Math.Clamp(LeftThumb.X + GamepadThumb.X, short.MinValue, short.MaxValue));
                                        LeftThumb.Y = (short)(Math.Clamp(LeftThumb.Y + GamepadThumb.Y, short.MinValue, short.MaxValue));
                                        break;
                                }
                            }
                            break;

                        case Input.JoystickSteering:
                            {
                                float GamepadThumbX = InputUtils.Steering(
                                    xinputController.sensorFusion.DeviceAngle.Y,
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

                // Apply user defined in game deadzone setting compensation
                switch (ControllerService.currentProfile.umc_output)
                {
                    default:
                    case Output.RightStick:
                        RightThumb = InputUtils.ApplyAntiDeadzone(RightThumb, ControllerService.currentProfile.antideadzone);
                        break;
                    case Output.LeftStick:
                        LeftThumb = InputUtils.ApplyAntiDeadzone(LeftThumb, ControllerService.currentProfile.antideadzone);
                        break;
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
        }
    }
}