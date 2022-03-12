using ControllerCommon;
using ControllerService.Sensors;
using ControllerService.Targets;
using Microsoft.Extensions.Logging;
using SharpDX.DirectInput;
using SharpDX.XInput;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Numerics;
using System.Timers;

namespace ControllerService
{
    public class XInputController
    {
        public Controller physicalController;
        public ViGEmTarget virtualTarget;

        public Gamepad Gamepad;

        public Profile profile;
        private Profile defaultProfile;

        public Vector3 Acceleration;
        public Vector3 Angle;
        public Vector3 AngularUniversal;
        public Vector3 AngularVelocity;

        public Timer AngularVelocityTimer;

        public MultimediaTimer UpdateTimer;
        public float WidhtHeightRatio = 2.5f;
        public double vibrationStrength = 100.0d;
        public int updateInterval = 15;

        public DeviceInstance Instance;

        public XInputGirometer Gyrometer;
        public XInputAccelerometer Accelerometer;
        public XInputInclinometer Inclinometer;
        public SensorFusion sensorFusion;

        protected readonly Stopwatch stopwatch;
        public long microseconds;

        public DS4Touch Touch;

        public event UpdatedEventHandler Updated;
        public delegate void UpdatedEventHandler(XInputController controller);

        protected object updateLock = new();
        public UserIndex UserIndex;
        private readonly ILogger logger;
        private readonly PipeServer pipeServer;

        public XInputController(Controller controller, UserIndex index, ILogger logger, PipeServer pipeServer)
        {
            this.logger = logger;
            this.pipeServer = pipeServer;

            // initilize controller
            this.physicalController = controller;
            this.UserIndex = index;

            // initialize sensor(s)
            UpdateSensors();

            // initialize sensorfusion
            sensorFusion = new SensorFusion(logger);

            // initialize vectors
            AngularVelocity = new();
            Acceleration = new();
            Angle = new();

            AngularVelocityTimer = new Timer() { Enabled = false, AutoReset = false };
            AngularVelocityTimer.Elapsed += AngularVelocityTimer_Elapsed;

            // initialize profile(s)
            profile = new();
            defaultProfile = new();

            // initialize touch
            Touch = new();

            // initialize stopwatch
            stopwatch = new Stopwatch();
            stopwatch.Start();

            // initialize timers
            UpdateTimer = new MultimediaTimer(updateInterval);
            UpdateTimer.Tick += UpdateTimer_Ticked;
            UpdateTimer.Start();
        }

        public void UpdateSensors()
        {
            Gyrometer = new XInputGirometer(this, logger, pipeServer);
            Accelerometer = new XInputAccelerometer(this, logger, pipeServer);
            Inclinometer = new XInputInclinometer(this, logger, pipeServer);
        }

        private Stopwatch sw = new();
        private void UpdateTimer_Ticked(object sender, EventArgs e)
        {
            // update timestamp
            microseconds = stopwatch.ElapsedMilliseconds * 1000L;

            // get current gamepad state
            State state = physicalController.GetState();
            Gamepad = state.Gamepad;

            lock (updateLock)
            {
                // debug
                Debug.WriteLine(sw.ElapsedMilliseconds);
                sw.Restart();

                // update reading(s)
                AngularVelocity = AngularUniversal = Gyrometer.GetCurrentReading();
                Acceleration = Accelerometer.GetCurrentReading();
                Angle = Inclinometer.GetCurrentReading();

                /* reset timer(s)
                AngularVelocityTimer?.Stop();
                AngularVelocityTimer?.Start(); */

                // update virtual controller
                virtualTarget?.UpdateReport();
                sensorFusion?.UpdateReport(AngularVelocity, Acceleration);

                Updated?.Invoke(this);
            }
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

        public void SetProfile(Profile profile)
        {
            // skip if current profile
            if (profile == this.profile)
                return;

            // restore default profile
            if (profile == null)
                profile = defaultProfile;

            this.profile = profile;

            // update default profile
            if (profile.IsDefault)
                defaultProfile = profile;
            else
                logger.LogInformation("Profile {0} applied.", profile.name);
        }

        private void AngularVelocityTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            // Disable drift compensation for angle values. 
            // AngularVelocity = new();
            AngularUniversal = new();
        }

        public void SetWidthHeightRatio(int ratio)
        {
            WidhtHeightRatio = ((float)ratio) / 10;
            logger.LogInformation("Device width height ratio set to {0}", WidhtHeightRatio);
        }

        public void SetPollRate(int HIDrate)
        {
            updateInterval = HIDrate;

            UpdateTimer.Interval = HIDrate;
            AngularVelocityTimer.Interval = HIDrate * 4;
        }

        public void SetVibrationStrength(double strength)
        {
            vibrationStrength = strength;
            this.virtualTarget?.SetVibrationStrength(vibrationStrength);
        }

        public void SetViGEmTarget(ViGEmTarget target)
        {
            this.virtualTarget = target;

            SetPollRate(updateInterval);
            SetVibrationStrength(vibrationStrength);

            logger.LogInformation("Virtual {0} attached to {1} on slot {2}", target, Instance.InstanceName, UserIndex);
        }
    }
}