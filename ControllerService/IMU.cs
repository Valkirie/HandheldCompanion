using ControllerCommon;
using ControllerCommon.Controllers;
using ControllerCommon.Utils;
using ControllerService.Sensors;
using PrecisionTiming;
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

        public static PrecisionTimer UpdateTimer;
        public const int UpdateInterval = 10;

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

        public static bool IsInitialized;

        public static event InitializedEventHandler Initialized;
        public delegate void InitializedEventHandler();

        static IMU()
        {
            // initialize sensorfusion and madgwick
            sensorFusion = new SensorFusion();
            madgwickAHRS = new MadgwickAHRS(0.01f, 0.1f);

            // initialize stopwatch
            stopwatch = new Stopwatch();

            // initialize timers
            UpdateTimer = new PrecisionTimer();
            UpdateTimer.SetInterval(UpdateInterval);
            UpdateTimer.SetAutoResetMode(true);
        }

        public static void SetSensorFamily(SensorFamily sensorFamily)
        {
            // initialize sensors
            Gyrometer = new IMUGyrometer(sensorFamily, UpdateInterval);
            Accelerometer = new IMUAccelerometer(sensorFamily, UpdateInterval);
            Inclinometer = new IMUInclinometer(sensorFamily, UpdateInterval);
        }

        public static void Start()
        {
            stopwatch.Start();

            UpdateTimer.Tick += ComputeMovements;
            UpdateTimer.Start();

            IsInitialized = true;
            Initialized?.Invoke();
        }

        public static void Stop()
        {
            // halt sensors
            Gyrometer?.StopListening();
            Accelerometer?.StopListening();
            Inclinometer?.StopListening();

            UpdateTimer.Tick -= ComputeMovements;
            UpdateTimer.Stop();

            stopwatch.Stop();

            IsInitialized = false;
        }

        public static void Restart(bool update)
        {
            Stop();

            // force update sensors
            if (update)
            {
                Gyrometer.UpdateSensor();
                Accelerometer.UpdateSensor();
                Inclinometer.UpdateSensor();
            }

            Start();
        }

        public static void UpdateMovements(ControllerMovements movements)
        {
            Gyrometer.ReadingChanged(movements.GyroRoll, movements.GyroPitch, movements.GyroYaw);
            Accelerometer.ReadingChanged(movements.GyroAccelX, movements.GyroAccelY, movements.GyroAccelZ);
            Inclinometer.ReadingChanged(movements.GyroAccelX, movements.GyroAccelY, movements.GyroAccelZ);
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

                // async update client(s)
                Task.Run(() =>
                {
                    switch (ControllerService.CurrentTag)
                    {
                        case "SettingsMode0":
                            PipeServer.SendMessage(new PipeSensor(AngularVelocity[XInputSensorFlags.Centered], SensorType.Girometer));
                            break;

                        case "SettingsMode1":
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

                Updated?.Invoke();

                Monitor.Exit(updateLock);
            }
        }
    }
}