using ControllerCommon;
using ControllerService.Targets;
using Microsoft.Extensions.Logging;
using SharpDX.DirectInput;
using SharpDX.XInput;
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

        private Vector3 prevAcceleration;
        public Vector3 Acceleration;
        public Timer AccelerationTimer;

        private Vector3 prevAngularVelocity;
        public Vector3 AngularVelocity;
        public Timer AngularVelocityTimer;

        public Timer UpdateTimer;
        public float WidhtHeightRatio = 2.5f;
        public float vibrationStrength = 100.0f;
        public int updateInterval = 10;

        public DeviceInstance Instance;

        public XInputGirometer Gyrometer;
        public XInputAccelerometer Accelerometer;
        public XInputInclinometer Inclinometer;

        protected readonly Stopwatch stopwatch;
        public long microseconds;

        public DS4Touch Touch;

        public event UpdatedEventHandler Updated;
        public delegate void UpdatedEventHandler(XInputController controller);

        protected object updateLock = new();
        public UserIndex UserIndex;
        private readonly ILogger logger;

        public XInputController(Controller controller, UserIndex index, ILogger logger)
        {
            this.logger = logger;

            // initilize controller
            this.physicalController = controller;
            this.UserIndex = index;

            // initialize vectors
            AngularVelocity = new();
            Acceleration = new();

            AccelerationTimer = new Timer() { Enabled = false, AutoReset = false };
            AccelerationTimer.Elapsed += AccelerationTimer_Elapsed;

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
            UpdateTimer = new Timer() { Enabled = true, AutoReset = true };
            UpdateTimer.Elapsed += UpdateTimer_Elapsed;
        }

        private void UpdateTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            lock (updateLock)
            {
                // get current gamepad state
                State state = physicalController.GetState();
                Gamepad = state.Gamepad;

                // update timestamp
                microseconds = (long)(stopwatch.ElapsedTicks / (Stopwatch.Frequency / (1000L * 1000L)));

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

        public void SetGyroscope(XInputGirometer gyrometer)
        {
            Gyrometer = gyrometer;
            Gyrometer.ReadingChanged += Girometer_ReadingChanged;
        }

        public void SetAccelerometer(XInputAccelerometer accelerometer)
        {
            Accelerometer = accelerometer;
            Accelerometer.ReadingHasChanged += Accelerometer_ReadingChanged;
        }

        public void Accelerometer_ReadingChanged(XInputAccelerometer sender, Vector3 Acceleration)
        {
            this.Acceleration = Acceleration;

            AccelerationTimer?.Stop();
            AccelerationTimer?.Start();
        }

        private void AccelerationTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            Acceleration = new();
        }

        public void Girometer_ReadingChanged(XInputGirometer sender, Vector3 AngularVelocity)
        {
            this.AngularVelocity = AngularVelocity;

            AngularVelocityTimer?.Stop();
            AngularVelocityTimer?.Start();
        }

        private void AngularVelocityTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            AngularVelocity = new();
        }

        public void SetInclinometer(XInputInclinometer inclinometer)
        {
            Inclinometer = inclinometer;
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
            AccelerationTimer.Interval = HIDrate * 4;
            AngularVelocityTimer.Interval = HIDrate * 4;

            this.virtualTarget?.SetPollRate(updateInterval);
        }

        public void SetVibrationStrength(float strength)
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
