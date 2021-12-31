using ControllerCommon;
using ControllerService.Targets;
using Microsoft.Extensions.Logging;
using SharpDX.DirectInput;
using SharpDX.XInput;
using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;
using System.Timers;

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
        private object updateLock = new();

        private readonly ILogger logger;

        public XInputController(Controller controller, UserIndex index, int HIDrate, ILogger logger)
        {
            this.logger = logger;

            // initilize controller
            this.Controller = controller;
            this.UserIndex = index;

            // initialize timers
            UpdateTimer = new Timer(HIDrate) { Enabled = false, AutoReset = true };
        }

        public void SetPollRate(int HIDrate)
        {
            UpdateTimer.Interval = HIDrate;
            logger.LogInformation("Virtual {0} report interval set to {1}ms", Target.GetType().Name, UpdateTimer.Interval);
        }

        public void SetVibrationStrength(float strength)
        {
            this.Target.strength = strength / 100.0f;
            logger.LogInformation("Virtual {0} vibration strength set to {1}%", Target.GetType().Name, strength);
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

            logger.LogInformation("Virtual {0} attached to {1} on slot {2}", target.HID, Instance.InstanceName, UserIndex);
            logger.LogInformation("Virtual {0} report interval set to {1}ms", target.HID, UpdateTimer.Interval);

            switch (Target.HID)
            {
                case HIDmode.Xbox360Controller:
                    ((Xbox360Target)Target)?.Connect();
                    break;
                case HIDmode.DualShock4Controller:
                    ((DualShock4Target)Target)?.Connect();
                    break;
            }

            UpdateTimer.Elapsed += async (sender, e) => await UpdateReport();
            UpdateTimer.Enabled = true;
            UpdateTimer.Start();
        }

        private Task UpdateReport()
        {
            lock (updateLock)
            {
                switch (Target.HID)
                {
                    case HIDmode.Xbox360Controller:
                        ((Xbox360Target)Target)?.UpdateReport();
                        break;
                    case HIDmode.DualShock4Controller:
                        ((DualShock4Target)Target)?.UpdateReport();
                        break;
                }

                DSUServer?.NewReportIncoming(Target);
            }

            return Task.CompletedTask;
        }
    }
}
