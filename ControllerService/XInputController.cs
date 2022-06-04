using ControllerCommon;
using ControllerCommon.Managers;
using ControllerCommon.Utils;
using ControllerService.Sensors;
using ControllerService.Targets;
using SharpDX.XInput;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Numerics;
using System.Threading.Tasks;
using static ControllerCommon.Utils.DeviceUtils;

namespace ControllerService
{
    public class XInputController
    {
        public ControllerEx controllerEx;
        public string ProductName = "XInput Controller for Windows";

        public ViGEmTarget virtualTarget;

        public Gamepad Gamepad;
        private State GamepadState;

        public Dictionary<XInputSensorFlags, Vector3> Accelerations = new();
        public Dictionary<XInputSensorFlags, Vector3> AngularVelocities = new();

        public Vector3 Angle;

        public MultimediaTimer UpdateTimer;
        public double vibrationStrength = 100.0d;
        public int updateInterval = 10;

        private SensorFamily sensorFamily = SensorFamily.None;
        public XInputGirometer Gyrometer;
        public XInputAccelerometer Accelerometer;
        public XInputInclinometer Inclinometer;

        public SensorFusion sensorFusion;
        public MadgwickAHRS madgwickAHRS;

        protected Stopwatch stopwatch;
        public long CurrentMicroseconds;

        public static double TotalMilliseconds;
        public static double UpdateTimePreviousMilliseconds;
        public static double DeltaSeconds = 100.0d;

        public DS4Touch Touch;

        public event UpdatedEventHandler Updated;
        public delegate void UpdatedEventHandler(XInputController controller);

        protected object updateLock = new();
        private readonly PipeServer pipeServer;

        public XInputController(SensorFamily sensorFamily, PipeServer pipeServer)
        {
            this.pipeServer = pipeServer;

            // initialize sensorfusion and madgwick
            sensorFusion = new SensorFusion();
            madgwickAHRS = new MadgwickAHRS(0.01f, 0.1f);

            // initialize sensors
            Gyrometer = new XInputGirometer(sensorFamily, updateInterval);
            Accelerometer = new XInputAccelerometer(sensorFamily, updateInterval);
            Inclinometer = new XInputInclinometer(sensorFamily, updateInterval);
            this.sensorFamily = sensorFamily;

            // initialize vectors
            Accelerations = new();
            AngularVelocities = new();
            Angle = new();

            // initialize touch
            Touch = new();

            // initialize stopwatch
            stopwatch = new Stopwatch();

            // initialize timers
            UpdateTimer = new MultimediaTimer(updateInterval);
        }

        public void StartListening()
        {
            stopwatch.Start();

            UpdateTimer.Tick += UpdateTimer_Ticked;
            UpdateTimer.Start();
        }

        public void StopListening()
        {
            stopwatch.Stop();

            UpdateTimer.Tick -= UpdateTimer_Ticked;
            UpdateTimer.Stop();
        }

        public void UpdateSensors()
        {
            Gyrometer.UpdateSensor(sensorFamily);
            Accelerometer.UpdateSensor(sensorFamily);
            Inclinometer.UpdateSensor(sensorFamily);
        }

        public void SetController(ControllerEx controllerEx)
        {
            // initilize controller
            this.controllerEx = controllerEx;
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
                foreach (XInputSensorFlags flags in (XInputSensorFlags[])Enum.GetValues(typeof(XInputSensorFlags)))
                {
                    switch (flags)
                    {
                        case XInputSensorFlags.Default:
                            AngularVelocities[flags] = Gyrometer.GetCurrentReading();
                            Accelerations[flags] = Accelerometer.GetCurrentReading();
                            break;

                        case XInputSensorFlags.RawValue:
                            AngularVelocities[flags] = Gyrometer.GetCurrentReadingRaw();
                            Accelerations[flags] = Accelerometer.GetCurrentReadingRaw();
                            break;

                        case XInputSensorFlags.Centered:
                            AngularVelocities[flags] = Gyrometer.GetCurrentReading(true);
                            Accelerations[flags] = Accelerometer.GetCurrentReading(true);
                            break;

                        case XInputSensorFlags.WithRatio:
                            AngularVelocities[flags] = Gyrometer.GetCurrentReading(false, true);
                            Accelerations[flags] = Accelerometer.GetCurrentReading(false, false);
                            break;

                        case XInputSensorFlags.CenteredRatio:
                            AngularVelocities[flags] = Gyrometer.GetCurrentReading(true, true);
                            Accelerations[flags] = Accelerometer.GetCurrentReading(true, false);
                            break;

                        case XInputSensorFlags.CenteredRaw:
                            AngularVelocities[flags] = Gyrometer.GetCurrentReadingRaw(true);
                            Accelerations[flags] = Accelerometer.GetCurrentReadingRaw(true);
                            break;
                    }
                }

                Angle = Inclinometer.GetCurrentReading();

                // update sensorFusion (todo: call only when needed ?)
                sensorFusion.UpdateReport(TotalMilliseconds, DeltaSeconds, AngularVelocities[XInputSensorFlags.Centered], Accelerations[XInputSensorFlags.Default]);

#if DEBUG
                LogManager.LogDebug("Plot AccelerationRawX {0} {1}", TotalMilliseconds, Accelerations[XInputSensorFlags.RawValue].X);
                LogManager.LogDebug("Plot AccelerationRawY {0} {1}", TotalMilliseconds, Accelerations[XInputSensorFlags.RawValue].Y);
                LogManager.LogDebug("Plot AccelerationRawZ {0} {1}", TotalMilliseconds, Accelerations[XInputSensorFlags.RawValue].Z);

                LogManager.LogDebug("Plot AngRawX {0} {1}", TotalMilliseconds, AngularVelocities[XInputSensorFlags.RawValue].X);
                LogManager.LogDebug("Plot AngRawY {0} {1}", TotalMilliseconds, AngularVelocities[XInputSensorFlags.RawValue].Y);
                LogManager.LogDebug("Plot AngRawZ {0} {1}", TotalMilliseconds, AngularVelocities[XInputSensorFlags.RawValue].Z);
#endif

                // async update client(s)
                Task.Run(() =>
                {
                    switch (ControllerService.CurrentTag)
                    {
                        case "ProfileSettingsMode0":
                            pipeServer?.SendMessage(new PipeSensor(AngularVelocities[XInputSensorFlags.Centered], SensorType.Girometer));
                            break;

                        case "ProfileSettingsMode1":
                            pipeServer?.SendMessage(new PipeSensor(Angle, SensorType.Inclinometer));
                            break;
                    }

                    switch (ControllerService.CurrentOverlayStatus)
                    {
                        case 0: // Visible
                            var AngularVelocityRad = new Vector3();
                            AngularVelocityRad.X = -InputUtils.deg2rad(AngularVelocities[XInputSensorFlags.CenteredRaw].X);
                            AngularVelocityRad.Y = -InputUtils.deg2rad(AngularVelocities[XInputSensorFlags.CenteredRaw].Y);
                            AngularVelocityRad.Z = -InputUtils.deg2rad(AngularVelocities[XInputSensorFlags.CenteredRaw].Z);
                            madgwickAHRS.UpdateReport(AngularVelocityRad.X, AngularVelocityRad.Y, AngularVelocityRad.Z, -Accelerations[XInputSensorFlags.RawValue].X, Accelerations[XInputSensorFlags.RawValue].Y, Accelerations[XInputSensorFlags.RawValue].Z, DeltaSeconds);

                            pipeServer?.SendMessage(new PipeSensor(madgwickAHRS.GetEuler(), madgwickAHRS.GetQuaternion(), SensorType.Quaternion));
                            break;
                    }
                });

#if DEBUG
                LogManager.LogDebug("Plot AccelerationRawX {0} {1}", TotalMilliseconds, Accelerations[XInputSensorFlags.RawValue].X);
                LogManager.LogDebug("Plot AccelerationRawY {0} {1}", TotalMilliseconds, Accelerations[XInputSensorFlags.RawValue].Y);
                LogManager.LogDebug("Plot AccelerationRawZ {0} {1}", TotalMilliseconds, Accelerations[XInputSensorFlags.RawValue].Z);

                LogManager.LogDebug("Plot GyroRawCX {0} {1}", TotalMilliseconds, Accelerations[XInputSensorFlags.CenteredRaw].X);
                LogManager.LogDebug("Plot GyroRawCY {0} {1}", TotalMilliseconds, Accelerations[XInputSensorFlags.CenteredRaw].Y);
                LogManager.LogDebug("Plot GyroRawCZ {0} {1}", TotalMilliseconds, Accelerations[XInputSensorFlags.CenteredRaw].Z);

                LogManager.LogDebug("Plot PoseX {0} {1}", TotalMilliseconds, madgwickAHRS.GetEuler().X);
                LogManager.LogDebug("Plot PoseY {0} {1}", TotalMilliseconds, madgwickAHRS.GetEuler().Y);
                LogManager.LogDebug("Plot PoseZ {0} {1}", TotalMilliseconds, madgwickAHRS.GetEuler().Z);
#endif

                // get current gamepad state
                if (controllerEx != null && controllerEx.IsConnected())
                {
                    GamepadState = controllerEx.GetState();
                    Gamepad = GamepadState.Gamepad;

                    // update virtual controller
                    virtualTarget?.UpdateReport(Gamepad);
                }

                Updated?.Invoke(this);
            }
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

            LogManager.LogInformation("{0} attached to {1} on slot {2}", target, ProductName, controllerEx.Controller.UserIndex);
        }
    }
}