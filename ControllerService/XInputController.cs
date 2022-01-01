using ControllerService.Targets;
using Microsoft.Extensions.Logging;
using SharpDX.DirectInput;
using SharpDX.XInput;
using System.Collections.Generic;
using System.Numerics;

namespace ControllerService
{
    public class XInputController
    {
        public Controller Controller;
        public ViGEmTarget Target;

        public DeviceInstance Instance;

        private DSUServer DSUServer;

        public XInputGirometer gyrometer;
        public XInputAccelerometer accelerometer;

        private readonly Timer UpdateTimer;

        public UserIndex UserIndex;

        private readonly ILogger logger;

        public XInputController(Controller controller, UserIndex index, ILogger logger)
        {
            this.logger = logger;

            // initilize controller
            this.Controller = controller;
            this.UserIndex = index;
        }

        public void SetPollRate(int HIDrate)
        {
            this.Target.UpdateTimer.Interval = HIDrate;
            logger.LogInformation("Virtual {0} report interval set to {1}ms", this.Target, this.Target.UpdateTimer.Interval);
        }

        public void SetVibrationStrength(float strength)
        {
            this.Target.strength = strength / 100.0f;
            logger.LogInformation("Virtual {0} vibration strength set to {1}%", this.Target, strength);
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
            gyrometer = _gyrometer;
            gyrometer.ReadingChanged += Girometer_ReadingChanged;
        }

        public void SetAccelerometer(XInputAccelerometer _accelerometer)
        {
            accelerometer = _accelerometer;
            accelerometer.ReadingChanged += Accelerometer_ReadingChanged;
        }

        public void SetDSUServer(DSUServer _server)
        {
            DSUServer = _server;
        }

        private void Accelerometer_ReadingChanged(object sender, Vector3 acceleration)
        {
            Target.Acceleration = acceleration;
        }

        private void Girometer_ReadingChanged(object sender, Vector3 angularvelocity)
        {
            Target.AngularVelocity = angularvelocity;
        }

        public void SetTarget(ViGEmTarget target)
        {
            this.Target = target;
            Target.Submited += SubmitReport;

            logger.LogInformation("Virtual {0} attached to {1} on slot {2}", target, Instance.InstanceName, UserIndex);
            logger.LogInformation("Virtual {0} report interval set to {1}ms", target, this.Target.UpdateTimer.Interval);
        }

        private void SubmitReport(object sender)
        {
            DSUServer?.SubmitReport(Target);
        }
    }
}
