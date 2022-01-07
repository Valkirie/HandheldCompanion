using ControllerCommon;
using ControllerService.Targets;
using Microsoft.Extensions.Logging;
using SharpDX.DirectInput;
using SharpDX.XInput;
using System;
using System.Collections.Generic;
using System.Numerics;

namespace ControllerService
{
    public class XInputController
    {
        public Controller Controller;
        public ViGEmTarget Target;

        public DeviceInstance Instance;

        public XInputGirometer Gyrometer;
        public XInputAccelerometer Accelerometer;

        public UserIndex UserIndex;
        private readonly ILogger logger;

        public XInputController(Controller controller, UserIndex index, ILogger logger)
        {
            this.logger = logger;

            // initilize controller
            this.Controller = controller;
            this.UserIndex = index;
        }

        public Dictionary<string, string> ToArgs()
        {
            return new Dictionary<string, string>() {
                { "ProductName", Instance.ProductName },
                { "InstanceGuid", $"{Instance.InstanceGuid}" },
                { "ProductGuid", $"{Instance.ProductGuid}" },
                { "ProductIndex", $"{(int)UserIndex}" }
            };
        }

        public void SetGyroscope(XInputGirometer _gyrometer)
        {
            Gyrometer = _gyrometer;
            Gyrometer.ReadingChanged += Girometer_ReadingChanged;
        }

        public void SetAccelerometer(XInputAccelerometer _accelerometer)
        {
            Accelerometer = _accelerometer;
            Accelerometer.ReadingChanged += Accelerometer_ReadingChanged;
        }

        private void Accelerometer_ReadingChanged(XInputAccelerometer sender, Vector3 acceleration)
        {
            Target.Acceleration = acceleration;
        }

        private void Girometer_ReadingChanged(XInputGirometer sender, Vector3 angularvelocity)
        {
            Target.AngularVelocity = angularvelocity;
        }

        public void SetTarget(ViGEmTarget target)
        {
            this.Target = target;

            switch (Target.HID)
            {
                default:
                case HIDmode.DualShock4Controller:
                    ((DualShock4Target)Target).Connect();
                    break;
                case HIDmode.Xbox360Controller:
                    ((Xbox360Target)Target).Connect();
                    break;
            }

            logger.LogInformation("Virtual {0} attached to {1} on slot {2}", target, Instance.InstanceName, UserIndex);
            logger.LogInformation("Virtual {0} report interval set to {1}ms", target, Target.UpdateTimer.Interval);
        }
    }
}
