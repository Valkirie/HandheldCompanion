using ControllerCommon.Managers;
using ControllerCommon.Sensors;
using ControllerCommon.Utils;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Timers;
using Windows.Devices.Sensors;
using static ControllerCommon.Utils.CommonUtils;
using static ControllerCommon.Utils.DeviceUtils;

namespace ControllerService.Sensors
{
    [Flags]
    public enum XInputSensorFlags
    {
        Default = 0000,
        RawValue = 0001,
        Centered = 0010,
        WithRatio = 0100,
        CenteredRaw = RawValue | Centered,
        CenteredRatio = RawValue | WithRatio,
    }

    [Flags]
    public enum XInputSensorStatus
    {
        Missing = 0,
        Ready = 1,
        Busy = 2
    }

    public abstract class IMUSensor
    {
        protected Vector3 reading = new();
        protected Vector3 reading_fixed = new();

        protected static SensorSpec sensorSpec;

        protected Timer centerTimer;
        protected int updateInterval;

        public object sensor;
        public OneEuroFilter3D filter = new();

        protected Dictionary<char, double> reading_axis = new Dictionary<char, double>()
        {
            { 'X', 0.0d },
            { 'Y', 0.0d },
            { 'Z', 0.0d },
        };

        protected IMUSensor()
        {
            this.centerTimer = new Timer() { Enabled = false, AutoReset = false, Interval = 100 };
            this.centerTimer.Elapsed += Timer_Elapsed;
        }

        protected virtual void ReadingChanged()
        {
            // reset reading after inactivity
            centerTimer.Stop();
            centerTimer.Start();
        }

        public static XInputSensorStatus GetStatus(SensorFamily sensorFamily)
        {
            switch (sensorFamily)
            {
                case SensorFamily.WindowsDevicesSensors:
                    {
                        var sensor = Gyrometer.GetDefault();
                        if (sensor != null)
                            return XInputSensorStatus.Busy;
                    }
                    break;
                case SensorFamily.SerialUSBIMU:
                    {
                        var sensor = SerialUSBIMU.GetDefault();
                        if (sensor != null)
                            return sensor.IsOpen() ? XInputSensorStatus.Busy : XInputSensorStatus.Ready;
                    }
                    break;
            }
            return XInputSensorStatus.Missing;
        }

        protected virtual void Timer_Elapsed(object sender, ElapsedEventArgs e)
        {
            this.reading_fixed.X = this.reading_fixed.Y = this.reading_fixed.Z = 0;
        }

        public override string ToString()
        {
            return this.GetType().Name;
        }

        protected virtual Vector3 GetCurrentReading(bool center = false, bool ratio = false)
        {
            return center ? this.reading_fixed : this.reading;
        }

        public Vector3 GetCurrentReadingRaw(bool center = false, bool ratio = false)
        {
            return center ? this.reading_fixed : this.reading;
        }
    }
}
