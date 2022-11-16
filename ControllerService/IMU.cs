using ControllerCommon;
using ControllerCommon.Managers;
using ControllerCommon.Utils;
using ControllerService.Sensors;
using PrecisionTiming;
using Serilog;
using Serilog.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using static ControllerCommon.Utils.DeviceUtils;

namespace ControllerService
{
    public static class IMU
    {
        public static Dictionary<XInputSensorFlags, Vector3> Acceleration = new();
        public static Dictionary<XInputSensorFlags, Vector3> AngularVelocity = new();
        public static Vector3 IMU_Angle = new();

        public static PrecisionTimer IMU_Timer;
        public static int IMU_Interval = 10;

        private static SensorFamily sensorFamily = SensorFamily.None;
        public static IMUGyrometer Gyrometer;
        public static IMUAccelerometer Accelerometer;
        public static IMUInclinometer Inclinometer;

        public static SensorFusion sensorFusion;
        public static MadgwickAHRS madgwickAHRS;

        public static Stopwatch stopwatch;
        public static long CurrentMicroseconds;

        public static double TotalMilliseconds;
        public static double UpdateTimePreviousMilliseconds;
        public static double DeltaSeconds = 100.0d;

        public static event UpdatedEventHandler Updated;
        public delegate void UpdatedEventHandler();

        private static object updateLock = new();

        public static void Initialize(SensorFamily sensorFamily)
        {
            // initialize sensorfusion and madgwick
            sensorFusion = new SensorFusion();
            madgwickAHRS = new MadgwickAHRS(0.01f, 0.1f);

            // initialize sensors
            Gyrometer = new IMUGyrometer(sensorFamily, IMU_Interval);
            Accelerometer = new IMUAccelerometer(sensorFamily, IMU_Interval);
            Inclinometer = new IMUInclinometer(sensorFamily, IMU_Interval);
            sensorFamily = sensorFamily;

            // initialize stopwatch
            stopwatch = new Stopwatch();

            // initialize timers
            IMU_Timer = new PrecisionTimer();
            IMU_Timer.SetInterval(IMU_Interval);
            IMU_Timer.SetAutoResetMode(true);
        }

        public static void StartListening()
        {
            stopwatch.Start();

            IMU_Timer.Tick += ComputeMovements;
            IMU_Timer.Start();
        }

        public static void StopListening()
        {
            Gyrometer.StopListening(sensorFamily);
            Accelerometer.StopListening(sensorFamily);
            Inclinometer.StopListening(sensorFamily);

            IMU_Timer.Tick -= ComputeMovements;
            IMU_Timer.Stop();

            stopwatch.Stop();
        }

        public static void UpdateSensors()
        {
            Gyrometer.UpdateSensor(sensorFamily);
            Accelerometer.UpdateSensor(sensorFamily);
            Inclinometer.UpdateSensor(sensorFamily);
        }

        private static void ComputeMovements(object sender, EventArgs e)
        {
            if (Monitor.TryEnter(updateLock))
            {
                // update timestamp
                CurrentMicroseconds = stopwatch.ElapsedMilliseconds * 1000L;
                TotalMilliseconds = stopwatch.Elapsed.TotalMilliseconds;
                DeltaSeconds = (TotalMilliseconds - UpdateTimePreviousMilliseconds) / 1000L;
                UpdateTimePreviousMilliseconds = TotalMilliseconds;

                // update reading(s)
                foreach (XInputSensorFlags flags in (XInputSensorFlags[])Enum.GetValues(typeof(XInputSensorFlags)))
                {
                    switch (flags)
                    {
                        case XInputSensorFlags.Default:
                            AngularVelocity[flags] = Gyrometer.GetCurrentReading();
                            Acceleration[flags] = Accelerometer.GetCurrentReading();
                            break;

                        case XInputSensorFlags.RawValue:
                            AngularVelocity[flags] = Gyrometer.GetCurrentReadingRaw();
                            Acceleration[flags] = Accelerometer.GetCurrentReadingRaw();
                            break;

                        case XInputSensorFlags.Centered:
                            AngularVelocity[flags] = Gyrometer.GetCurrentReading(true);
                            Acceleration[flags] = Accelerometer.GetCurrentReading(true);
                            break;

                        case XInputSensorFlags.WithRatio:
                            AngularVelocity[flags] = Gyrometer.GetCurrentReading(false, true);
                            Acceleration[flags] = Accelerometer.GetCurrentReading(false, false);
                            break;

                        case XInputSensorFlags.CenteredRatio:
                            AngularVelocity[flags] = Gyrometer.GetCurrentReading(true, true);
                            Acceleration[flags] = Accelerometer.GetCurrentReading(true, false);
                            break;

                        case XInputSensorFlags.CenteredRaw:
                            AngularVelocity[flags] = Gyrometer.GetCurrentReadingRaw(true);
                            Acceleration[flags] = Accelerometer.GetCurrentReadingRaw(true);
                            break;
                    }
                }

                IMU_Angle = Inclinometer.GetCurrentReading();

                // update sensorFusion (todo: call only when needed ?)
                sensorFusion.UpdateReport(TotalMilliseconds, DeltaSeconds, AngularVelocity[XInputSensorFlags.Centered], Acceleration[XInputSensorFlags.Default]);

#if DEBUG
                LogManager.LogDebug("Plot AccelerationRawX {0} {1}", TotalMilliseconds, Acceleration[XInputSensorFlags.RawValue].X);
                LogManager.LogDebug("Plot AccelerationRawY {0} {1}", TotalMilliseconds, Acceleration[XInputSensorFlags.RawValue].Y);
                LogManager.LogDebug("Plot AccelerationRawZ {0} {1}", TotalMilliseconds, Acceleration[XInputSensorFlags.RawValue].Z);

                LogManager.LogDebug("Plot AngRawX {0} {1}", TotalMilliseconds, AngularVelocity[XInputSensorFlags.RawValue].X);
                LogManager.LogDebug("Plot AngRawY {0} {1}", TotalMilliseconds, AngularVelocity[XInputSensorFlags.RawValue].Y);
                LogManager.LogDebug("Plot AngRawZ {0} {1}", TotalMilliseconds, AngularVelocity[XInputSensorFlags.RawValue].Z);
#endif

                // async update client(s)
                Task.Run(() =>
                {
                    switch (ControllerService.CurrentTag)
                    {
                        case "ProfileSettingsMode0":
                            PipeServer.SendMessage(new PipeSensor(AngularVelocity[XInputSensorFlags.Centered], SensorType.Girometer));
                            break;

                        case "ProfileSettingsMode1":
                            PipeServer.SendMessage(new PipeSensor(IMU_Angle, SensorType.Inclinometer));
                            break;
                    }

                    switch (ControllerService.CurrentOverlayStatus)
                    {
                        case 0: // Visible
                            var AngularVelocityRad = new Vector3();
                            AngularVelocityRad.X = -InputUtils.deg2rad(AngularVelocity[XInputSensorFlags.CenteredRaw].X);
                            AngularVelocityRad.Y = -InputUtils.deg2rad(AngularVelocity[XInputSensorFlags.CenteredRaw].Y);
                            AngularVelocityRad.Z = -InputUtils.deg2rad(AngularVelocity[XInputSensorFlags.CenteredRaw].Z);
                            madgwickAHRS.UpdateReport(AngularVelocityRad.X, AngularVelocityRad.Y, AngularVelocityRad.Z, -Acceleration[XInputSensorFlags.RawValue].X, Acceleration[XInputSensorFlags.RawValue].Y, Acceleration[XInputSensorFlags.RawValue].Z, DeltaSeconds);

                            PipeServer.SendMessage(new PipeSensor(madgwickAHRS.GetEuler(), madgwickAHRS.GetQuaternion(), SensorType.Quaternion));
                            break;
                    }
                });

#if DEBUG
                LogManager.LogDebug("Plot AccelerationRawX {0} {1}", TotalMilliseconds, Acceleration[XInputSensorFlags.RawValue].X);
                LogManager.LogDebug("Plot AccelerationRawY {0} {1}", TotalMilliseconds, Acceleration[XInputSensorFlags.RawValue].Y);
                LogManager.LogDebug("Plot AccelerationRawZ {0} {1}", TotalMilliseconds, Acceleration[XInputSensorFlags.RawValue].Z);

                LogManager.LogDebug("Plot GyroRawCX {0} {1}", TotalMilliseconds, Acceleration[XInputSensorFlags.CenteredRaw].X);
                LogManager.LogDebug("Plot GyroRawCY {0} {1}", TotalMilliseconds, Acceleration[XInputSensorFlags.CenteredRaw].Y);
                LogManager.LogDebug("Plot GyroRawCZ {0} {1}", TotalMilliseconds, Acceleration[XInputSensorFlags.CenteredRaw].Z);

                LogManager.LogDebug("Plot PoseX {0} {1}", TotalMilliseconds, madgwickAHRS.GetEuler().X);
                LogManager.LogDebug("Plot PoseY {0} {1}", TotalMilliseconds, madgwickAHRS.GetEuler().Y);
                LogManager.LogDebug("Plot PoseZ {0} {1}", TotalMilliseconds, madgwickAHRS.GetEuler().Z);
#endif

                Updated?.Invoke();

                Monitor.Exit(updateLock);
            }
        }
    }
}