using System;
using System.Numerics;
using ControllerCommon;
using ControllerCommon.Controllers;
using ControllerCommon.Inputs;
using ControllerCommon.Managers;
using ControllerCommon.Utils;
using ControllerService.Sensors;
using Nefarius.ViGEm.Client;

namespace ControllerService.Targets;

public abstract class ViGEmTarget : IDisposable
{
    public delegate void ConnectedEventHandler(ViGEmTarget target);

    public delegate void DisconnectedEventHandler(ViGEmTarget target);

    public FlickStick flickStick;

    public HIDmode HID = HIDmode.NoController;
    protected ControllerState Inputs = new();

    public bool IsConnected;
    protected bool IsSilenced;

    protected Vector2 LeftThumb;
    protected Vector2 RightThumb;

    protected IVirtualGamepad virtualController;

    protected ViGEmTarget()
    {
        // initialize flick stick
        flickStick = new FlickStick();

        ControllerService.ForegroundUpdated += ForegroundUpdated;
    }

    public virtual void Dispose()
    {
        Disconnect();
        GC.SuppressFinalize(this);
    }

    public event ConnectedEventHandler Connected;

    public event DisconnectedEventHandler Disconnected;

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
        IsConnected = false;
        Disconnected?.Invoke(this);
        LogManager.LogInformation("{0} disconnected", ToString());
    }

    public void UpdateInputs(ControllerState inputs)
    {
        Inputs = inputs;
    }

    public virtual void UpdateReport(long ticks)
    {
        // get sticks values
        LeftThumb = new Vector2(Inputs.AxisState[AxisFlags.LeftThumbX], Inputs.AxisState[AxisFlags.LeftThumbY]);
        RightThumb = new Vector2(Inputs.AxisState[AxisFlags.RightThumbX], Inputs.AxisState[AxisFlags.RightThumbY]);

        if (ControllerService.currentProfile.MotionEnabled && Inputs.MotionTriggered)
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
                            Angular = new Vector2((float)IMU.sensorFusion.CameraYawDelta,
                                (float)IMU.sensorFusion.CameraPitchDelta);
                            break;
                        case MotionInput.AutoRollYawSwap:
                            Angular = InputUtils.AutoRollYawSwap(IMU.sensorFusion.GravityVectorSimple,
                                IMU.AngularVelocity[XInputSensorFlags.Centered]);
                            break;
                        default:
                        case MotionInput.JoystickCamera:
                            Angular = new Vector2(-IMU.AngularVelocity[XInputSensorFlags.Centered].Z,
                                IMU.AngularVelocity[XInputSensorFlags.Centered].X);
                            break;
                    }

                    // apply sensivity curve
                    Angular.X *= InputUtils.ApplyCustomSensitivity(Angular.X, IMUGyrometer.sensorSpec.maxIn,
                        ControllerService.currentProfile.MotionSensivityArray);
                    Angular.Y *= InputUtils.ApplyCustomSensitivity(Angular.Y, IMUGyrometer.sensorSpec.maxIn,
                        ControllerService.currentProfile.MotionSensivityArray);

                    // apply aiming down scopes multiplier if activated
                    if (Inputs.ButtonState.Contains(ControllerService.currentProfile.AimingSightsTrigger))
                        Angular *= ControllerService.currentProfile.AimingSightsMultiplier;

                    // apply sensivity
                    var GamepadThumb = new Vector2(
                        Angular.X * ControllerService.currentProfile.GetSensitivityX(),
                        Angular.Y * ControllerService.currentProfile.GetSensitivityY());

                    // apply anti deadzone to motion based thumb input to overcome deadzone and experience small movements properly
                    GamepadThumb = InputUtils.ApplyAntiDeadzone(GamepadThumb,
                        ControllerService.currentProfile.MotionAntiDeadzone);

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
                                var FlickStickX = flickStick.Handle(RightThumb,
                                    ControllerService.currentProfile.FlickstickDuration,
                                    ControllerService.currentProfile.FlickstickSensivity,
                                    IMU.TotalMilliseconds);

                                // X input combines motion controls plus flick stick result
                                // Y input only from motion controls
                                RightThumb.X = (short)Math.Clamp(GamepadThumb.X - FlickStickX, short.MinValue,
                                    short.MaxValue);
                                RightThumb.Y = (short)Math.Clamp(GamepadThumb.Y, short.MinValue, short.MaxValue);
                            }
                            else
                            {
                                RightThumb.X = (short)Math.Clamp(RightThumb.X + GamepadThumb.X, short.MinValue,
                                    short.MaxValue);
                                RightThumb.Y = (short)Math.Clamp(RightThumb.Y + GamepadThumb.Y, short.MinValue,
                                    short.MaxValue);
                            }

                            break;

                        case MotionOutput.LeftStick:
                            LeftThumb.X = (short)Math.Clamp(LeftThumb.X + GamepadThumb.X, short.MinValue,
                                short.MaxValue);
                            LeftThumb.Y = (short)Math.Clamp(LeftThumb.Y + GamepadThumb.Y, short.MinValue,
                                short.MaxValue);

                            break;
                    }
                }
                    break;

                case MotionInput.JoystickSteering:
                {
                    var GamepadThumbX = InputUtils.Steering(
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

    internal void SubmitReport()
    {
        /*
         * ButtonsInjector = 0;
         * sStateInjector.wButtons = 0;
         */
    }
}