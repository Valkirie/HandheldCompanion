using ControllerCommon;
using ControllerCommon.Devices;
using ControllerCommon.Utils;
using ControllerService.Sensors;
using ControllerService.Targets;
using Microsoft.Extensions.Logging;
using SharpDX.XInput;
using System;
using System.Diagnostics;
using System.Numerics;
using System.Threading.Tasks;

namespace ControllerService
{
    public class XInputController
    {
        public ControllerEx controllerEx;
        public string ProductName = "XInput Controller for Windows";

        public ViGEmTarget virtualTarget;

        public Gamepad Gamepad;
        private Gamepad prevGamepad;
        private State GamepadState;

        public Profile profile;
        private Profile defaultProfile;

        public Vector3 Acceleration;
        public Vector3 AccelerationRaw;
        public Vector3 AccelerationRatio;

        public Vector3 Angle;
        public Vector3 AngularVelocityC;
        public Vector3 AngularVelocityRatio;
        public Vector3 AngularVelocity;
        public Vector3 AngularVelocityRad;
        public Vector3 AngularRawC;

        public MultimediaTimer UpdateTimer;
        public double vibrationStrength = 100.0d;
        public int updateInterval = 10;

        public XInputGirometer Gyrometer;
        public XInputAccelerometer Accelerometer;
        public XInputInclinometer Inclinometer;

        public SensorFusion sensorFusion;
        public MadgwickAHRS madgwickAHRS;

        public Device handheldDevice;

        protected readonly Stopwatch stopwatch;
        public long CurrentMicroseconds;

        public double TotalMilliseconds;
        public double UpdateTimePreviousMilliseconds;
        public double DeltaSeconds;

        public DS4Touch Touch;

        public event UpdatedEventHandler Updated;
        public delegate void UpdatedEventHandler(XInputController controller);

        protected object updateLock = new();
        private readonly ILogger logger;
        private readonly PipeServer pipeServer;

        public XInputController(ILogger logger, PipeServer pipeServer)
        {
            this.logger = logger;
            this.pipeServer = pipeServer;

            // initialize sensor(s)
            UpdateSensors();

            // initialize vectors
            AngularVelocity = new();
            AngularVelocityRad = new();
            AngularRawC = new();
            Acceleration = new();
            Angle = new();

            // initialize sensorfusion and madgwick
            sensorFusion = new SensorFusion(logger);
            madgwickAHRS = new MadgwickAHRS(0.01f, 0.1f);

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

        internal void SetController(ControllerEx controllerEx)
        {
            // initilize controller
            this.controllerEx = controllerEx;
        }

        public void UpdateSensors()
        {
            Gyrometer = new XInputGirometer(this, logger);
            Accelerometer = new XInputAccelerometer(this, logger);
            Inclinometer = new XInputInclinometer(this, logger);
        }

        private void UpdateTimer_Ticked(object sender, EventArgs e)
        {
            // update timestamp
            CurrentMicroseconds = stopwatch.ElapsedMilliseconds * 1000L;
            TotalMilliseconds = stopwatch.Elapsed.TotalMilliseconds;
            DeltaSeconds = (TotalMilliseconds - UpdateTimePreviousMilliseconds) / 1000L;
            UpdateTimePreviousMilliseconds = TotalMilliseconds;

            lock (updateLock)
            {
                // update reading(s)
                AngularVelocity = Gyrometer.GetCurrentReading();
                AngularVelocityC = Gyrometer.GetCurrentReading(true);
                AngularVelocityRatio = Gyrometer.GetCurrentReading(false, true);
                AngularRawC = Gyrometer.GetCurrentReadingRaw(true);

                Acceleration = Accelerometer.GetCurrentReading();
                AccelerationRaw = Accelerometer.GetCurrentReadingRaw(false);
                AccelerationRatio = Accelerometer.GetCurrentReading(false, true);

                Angle = Inclinometer.GetCurrentReading();

                // update sensorFusion (todo: call only when needed ?)
                sensorFusion.UpdateReport(TotalMilliseconds, DeltaSeconds, AngularVelocity, Acceleration);

                // async update client(s)
                Task.Run(() =>
                {
                    switch (ControllerService.CurrentTag)
                    {
                        case "ProfileSettingsMode0":
                            pipeServer?.SendMessage(new PipeSensor(AngularVelocityC, SensorType.Girometer));
                            break;

                        case "ProfileSettingsMode1":
                            pipeServer?.SendMessage(new PipeSensor(Angle, SensorType.Inclinometer));
                            break;
                    }

                    switch (ControllerService.CurrentOverlayStatus)
                    {
                        case 0: // Visible
                            AngularVelocityRad.X = -InputUtils.deg2rad(AngularRawC.X);
                            AngularVelocityRad.Y = -InputUtils.deg2rad(AngularRawC.Y);
                            AngularVelocityRad.Z = -InputUtils.deg2rad(AngularRawC.Z);
                            madgwickAHRS.UpdateReport(AngularVelocityRad.X, AngularVelocityRad.Y, AngularVelocityRad.Z, -AccelerationRaw.X, AccelerationRaw.Y, AccelerationRaw.Z, DeltaSeconds);

                            pipeServer?.SendMessage(new PipeSensor(madgwickAHRS.GetEuler(), madgwickAHRS.GetQuaternion(), SensorType.Quaternion));
                            break;
                        case 1: // Hidden
                        case 2: // Collapsed
                            madgwickAHRS = new MadgwickAHRS(0.01f, 0.1f);
                            pipeServer?.SendMessage(new PipeSensor(madgwickAHRS.GetEuler(), madgwickAHRS.GetQuaternion(), SensorType.Quaternion));
                            ControllerService.CurrentOverlayStatus = 3; // leave the loop
                            break;
                    }
                });

                Task.Run(() =>
                {
                    logger.LogDebug("Plot AccelerationRawX {0} {1}", TotalMilliseconds, AccelerationRaw.X);
                    logger.LogDebug("Plot AccelerationRawY {0} {1}", TotalMilliseconds, AccelerationRaw.Y);
                    logger.LogDebug("Plot AccelerationRawZ {0} {1}", TotalMilliseconds, AccelerationRaw.Z);

                    logger.LogDebug("Plot GyroRawCX {0} {1}", TotalMilliseconds, AngularRawC.X);
                    logger.LogDebug("Plot GyroRawCY {0} {1}", TotalMilliseconds, AngularRawC.Y);
                    logger.LogDebug("Plot GyroRawCZ {0} {1}", TotalMilliseconds, AngularRawC.Z);

                    logger.LogDebug("Plot PoseX {0} {1}", TotalMilliseconds, madgwickAHRS.GetEuler().X);
                    logger.LogDebug("Plot PoseY {0} {1}", TotalMilliseconds, madgwickAHRS.GetEuler().Y);
                    logger.LogDebug("Plot PoseZ {0} {1}", TotalMilliseconds, madgwickAHRS.GetEuler().Z);
                });

                // get current gamepad state
                if (controllerEx != null && controllerEx.IsConnected())
                {
                    GamepadState = controllerEx.GetState();
                    Gamepad = GamepadState.Gamepad;

                    if (prevGamepad.ToString() != Gamepad.ToString())
                        pipeServer?.SendMessage(new PipeGamepad(Gamepad));
                    prevGamepad = Gamepad;

                    // update virtual controller
                    virtualTarget?.UpdateReport(Gamepad);
                }

                Updated?.Invoke(this);
            }
        }

        internal void SetDevice(Device handheldDevice)
        {
            this.handheldDevice = handheldDevice;
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

        public void SetPollRate(int HIDrate)
        {
            updateInterval = HIDrate;
            UpdateTimer.Interval = HIDrate;
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

            logger.LogInformation("Virtual {0} attached to {1} on slot {2}", target, ProductName, controllerEx.Controller.UserIndex);
        }
    }
}