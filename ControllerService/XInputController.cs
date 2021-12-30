using ControllerCommon;
using ControllerService.Targets;
using Microsoft.Extensions.Logging;
using Nefarius.ViGEm.Client;
using Nefarius.ViGEm.Client.Targets;
using Nefarius.ViGEm.Client.Targets.DualShock4;
using Nefarius.ViGEm.Client.Targets.Xbox360;
using SharpDX.DirectInput;
using SharpDX.XInput;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Timers;
using static ControllerCommon.Utils;

namespace ControllerService
{
    public class XInputController
    {
        public Controller Controller;
        public ViGEmTarget target;

        public DeviceInstance instance;

        private DSUServer server;

        public XInputGirometer gyrometer;
        public XInputAccelerometer accelerometer;

        private readonly Timer UpdateTimer;
        private float strength;

        public UserIndex index;
        private object updateLock = new();

        private readonly ILogger logger;
        private readonly string HIDmode;

        public XInputController(Controller controller, UserIndex index, int HIDrate, string HIDmode, ILogger logger)
        {
            this.logger = logger;
            this.HIDmode = HIDmode;

            // initilize controller
            this.Controller = controller;
            this.index = index;

            // initialize timers
            UpdateTimer = new Timer(HIDrate) { Enabled = false, AutoReset = true };
        }

        public void SetPollRate(int HIDrate)
        {
            UpdateTimer.Interval = HIDrate;
            logger.LogInformation("Virtual {0} report interval set to {1}ms", target.GetType().Name, UpdateTimer.Interval);
        }

        public void SetVibrationStrength(float strength)
        {
            this.strength = strength / 100.0f;
            logger.LogInformation("Virtual {0} vibration strength set to {1}%", target.GetType().Name, strength);
        }

        public Dictionary<string, string> ToArgs()
        {
            return new Dictionary<string, string>() {
                { "ProductName", instance.ProductName },
                { "InstanceGuid", $"{instance.InstanceGuid}" },
                { "ProductGuid", $"{instance.ProductGuid}" },
                { "ProductIndex", $"{(int)index}" }
            };
        }

        private void XBOX_FeedbackReceived(object sender, Xbox360FeedbackReceivedEventArgs e)
        {
            if (!Controller.IsConnected)
                return;

            Vibration inputMotor = new()
            {
                LeftMotorSpeed = (ushort)((e.LargeMotor * ushort.MaxValue / byte.MaxValue) * strength),
                RightMotorSpeed = (ushort)((e.SmallMotor * ushort.MaxValue / byte.MaxValue) * strength),
            };
            Controller.SetVibration(inputMotor);
        }

        private void DS4_FeedbackReceived(object sender, DualShock4FeedbackReceivedEventArgs e)
        {
            if (!Controller.IsConnected)
                return;

            Vibration inputMotor = new()
            {
                LeftMotorSpeed = (ushort)((e.LargeMotor * ushort.MaxValue / byte.MaxValue) * strength),
                RightMotorSpeed = (ushort)((e.SmallMotor * ushort.MaxValue / byte.MaxValue) * strength),
            };
            Controller.SetVibration(inputMotor);
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
            server = _server;
        }

        private void Accelerometer_ReadingChanged(object sender, Vector3 acceleration)
        {
            target.Acceleration = acceleration;
        }

        private void Girometer_ReadingChanged(object sender, Vector3 angularvelocity)
        {
            target.AngularVelocity = angularvelocity;
        }

        public void SetTarget(ViGEmClient client)
        {
            switch (HIDmode)
            {
                default:
                case "DualShock4Controller":
                    target = new DualShock4Target(client, Controller, (int)index);
                    break;
                case "Xbox360Controller":
                    target = new Xbox360Target(client, Controller, (int)index);
                    break;
            }

            if (target == null)
            {
                logger.LogCritical("No Virtual controller detected. Application will stop");
                throw new InvalidOperationException();
            }

            UpdateTimer.Elapsed += async (sender, e) => await UpdateReport();
            UpdateTimer.Enabled = true;
            UpdateTimer.Start();

            logger.LogInformation("Virtual {0} connected", target.GetType().Name);
            logger.LogInformation("Virtual {0} attached to {1} on slot {2}", target.GetType().Name, instance.InstanceName, index);
            logger.LogInformation("Virtual {0} report interval set to {1}ms", target.GetType().Name, UpdateTimer.Interval);
        }

        private Task UpdateReport()
        {
            lock (updateLock)
            {
                // that suxx !
                switch (HIDmode)
                {
                    default:
                    case "DualShock4Controller":
                        ((DualShock4Target)target)?.UpdateReport();
                        break;
                    case "Xbox360Controller":
                        ((Xbox360Target)target)?.UpdateReport();
                        break;
                }
                server?.NewReportIncoming(target);
            }

            return Task.CompletedTask;
        }
    }
}
