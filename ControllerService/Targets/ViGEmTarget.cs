using ControllerCommon;
using Microsoft.Extensions.Logging;
using Nefarius.ViGEm.Client;
using SharpDX.XInput;
using System;
using System.Runtime.InteropServices;
using System.Timers;
using GamepadButtonFlags = ControllerCommon.GamepadButtonFlags;

namespace ControllerService.Targets
{
    public abstract class ViGEmTarget
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

        public Gamepad Gamepad;
        public HIDmode HID = HIDmode.None;

        protected readonly ILogger logger;

        public MadgwickAHRS madgwick;

        protected ViGEmClient client { get; }
        protected IVirtualGamepad virtualController;

        protected XInputStateSecret state_s;

        protected double vibrationStrength;

        protected int UserIndex;

        protected short LeftThumbX, LeftThumbY, RightThumbX, RightThumbY;
        public Timer UpdateTimer;

        public event ConnectedEventHandler Connected;
        public delegate void ConnectedEventHandler(ViGEmTarget target);

        public event DisconnectedEventHandler Disconnected;
        public delegate void DisconnectedEventHandler(ViGEmTarget target);

        protected object updateLock = new();
        protected bool isConnected;

        protected ViGEmTarget(XInputController xinput, ViGEmClient client, Controller controller, int index, ILogger logger)
        {
            this.logger = logger;
            this.xinputController = xinput;

            // initialize madgwick
            madgwick = new(1f / 14f, 0.1f);

            // initialize secret state
            state_s = new();

            // initialize controller
            this.client = client;
            this.physicalController = controller;

            // initialize timers
            UpdateTimer = new Timer() { Enabled = false, AutoReset = true };
        }

        protected void FeedbackReceived(object sender, EventArgs e)
        {
        }

        public void SetPollRate(int HIDrate)
        {
            UpdateTimer.Interval = HIDrate;
            logger.LogInformation("Virtual {0} report interval set to {1}ms", this, HIDrate);
        }

        public void SetVibrationStrength(double strength)
        {
            vibrationStrength = strength / 100.0f;
            logger.LogInformation("Virtual {0} vibration strength set to {1}%", this, strength);
        }

        public override string ToString()
        {
            return Utils.GetDescriptionFromEnumValue(this.HID);
        }

        public virtual void Connect()
        {
            UpdateTimer.Enabled = true;
            UpdateTimer.Start();

            isConnected = true;
            Connected?.Invoke(this);
            logger.LogInformation("Virtual {0} connected", ToString());
        }

        public virtual void Disconnect()
        {
            UpdateTimer.Enabled = false;
            UpdateTimer.Stop();

            isConnected = false;
            Disconnected?.Invoke(this);
            logger.LogInformation("Virtual {0} disconnected", ToString());
        }

        public virtual unsafe void UpdateReport(object sender, ElapsedEventArgs e)
        {
            lock (updateLock)
            {
                // get current gamepad state
                XInputGetStateSecret13(UserIndex, out state_s);
                State state = physicalController.GetState();
                Gamepad = state.Gamepad;

                // get buttons values
                GamepadButtonFlags buttons = (GamepadButtonFlags)Gamepad.Buttons;
                buttons |= (Gamepad.LeftTrigger > 0 ? GamepadButtonFlags.LeftTrigger : 0);
                buttons |= (Gamepad.RightTrigger > 0 ? GamepadButtonFlags.RightTrigger : 0);

                // get custom buttons values
                buttons |= xinputController.profile.umc_trigger.HasFlag(GamepadButtonFlags.AlwaysOn) ? GamepadButtonFlags.AlwaysOn : 0;

                // get sticks values
                LeftThumbX = Gamepad.LeftThumbX;
                LeftThumbY = Gamepad.LeftThumbY;
                RightThumbX = Gamepad.RightThumbX;
                RightThumbY = Gamepad.RightThumbY;

                if (xinputController.profile.umc_enabled && (xinputController.profile.umc_trigger & buttons) != 0)
                {
                    switch(xinputController.profile.umc_input)
                    {
                        case Input.JoystickCamera:
                            {
                                float intensity = xinputController.profile.GetIntensity();
                                float sensivity = xinputController.profile.GetSensiviy();

                                switch (xinputController.profile.umc_output)
                                {
                                    default:
                                    case Output.RightStick:
                                        RightThumbX = Utils.ComputeInput(RightThumbX, -xinputController.AngularVelocity.Z, sensivity, intensity);
                                        RightThumbY = Utils.ComputeInput(RightThumbY, xinputController.AngularVelocity.X, sensivity, intensity);
                                        break;
                                    case Output.LeftStick:
                                        LeftThumbX = Utils.ComputeInput(LeftThumbX, -xinputController.AngularVelocity.Z, sensivity, intensity);
                                        LeftThumbY = Utils.ComputeInput(LeftThumbY, xinputController.AngularVelocity.X, sensivity, intensity);
                                        break;
                                }
                            }
                            break;

                        case Input.JoystickSteering:
                            {
                                float user_defined_max_device_angle = xinputController.profile.steering_max_angle;
                                float to_the_power_of = xinputController.profile.steering_power;
                                float deadzone_angle = xinputController.profile.steering_deadzone;
                                float ingame_deadzone_setting_compensation = xinputController.profile.steering_deadzone_compensation;

                                // Range angle y value (0 to user defined angle) into -1.0 to 1.0 position value taking into account deadzone angle
                                float joystick_pos_capped_angle = Utils.AngleToJoystickPos(xinputController.Angle.Y, user_defined_max_device_angle, deadzone_angle);
                                logger?.LogDebug("Y, with max angle of {0:00.#}, ranged from -1.0 to 1: {1:0.####}, from angle: {2:0.####}", user_defined_max_device_angle, joystick_pos_capped_angle, xinputController.Angle.Y);

                                // Apply user defined to the power of to joystick pos
                                float joystick_pos_powered = Utils.DirectionRespectingPowerOf(joystick_pos_capped_angle, to_the_power_of);
                                logger?.LogDebug("DirectionRespectingPowerOf. Input: {0:0.#####} Power: {1:0.#} Result: {2:0.####}", joystick_pos_capped_angle, to_the_power_of, joystick_pos_powered);

                                // Apply user defined in game deadzone setting compensation
                                float joystick_pos_in_game_deadzone_compensated = Utils.InGameDeadZoneSettingCompensation(joystick_pos_powered, ingame_deadzone_setting_compensation);
                                logger?.LogDebug("InGameDeadZoneSettingCompensation. Input: {0:0.#####} Ingame Deadzone %: {1:0.#} Result: {2:0.####}", joystick_pos_powered, ingame_deadzone_setting_compensation, joystick_pos_in_game_deadzone_compensated);

                                // Scale joystick x pos -1 to 1 to joystick x range, send 0 for y.
                                switch (xinputController.profile.umc_output)
                                {
                                    default:
                                    case Output.RightStick:
                                        RightThumbX = (short)-(joystick_pos_in_game_deadzone_compensated * short.MaxValue);
                                        break;
                                    case Output.LeftStick:
                                        LeftThumbX = (short)-(joystick_pos_in_game_deadzone_compensated * short.MaxValue);
                                        break;
                                }

                                logger?.LogDebug("LeftThumbX: {0} based on device Y angle: {1:00.######} degs", LeftThumbX, xinputController.Angle.Y);
                            }
                            break;
                    }
                }
            }
        }

        internal void SubmitReport()
        {
            // do something
        }
    }
}
