using ControllerCommon.Utils;
using PrecisionTiming;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Timers;
using static ControllerCommon.Utils.CommonUtils;
using static ControllerCommon.Utils.DeviceUtils;

namespace ControllerService.Sensors
{
    [Flags]
    public enum XInputSensorFlags
    {
        Default = 0,
        RawValue = 1,
        Centered = 2,
        WithRatio = 4,
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

        protected PrecisionTimer centerTimer;
        protected int updateInterval;
        protected SensorFamily sensorFamily;

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
            this.centerTimer = new PrecisionTimer();
            this.centerTimer.SetInterval(100);
            this.centerTimer.SetAutoResetMode(false);
            this.centerTimer.Tick += Timer_Elapsed;
        }

        protected virtual void ReadingChanged()
        {
            if (centerTimer is null)
                return;

            // reset reading after inactivity
            this.centerTimer.Stop();
            this.centerTimer.Start();
        }

        protected virtual void StopListening()
        {
            if (centerTimer is null)
                return;

            this.centerTimer.Stop();
            this.centerTimer.Dispose();
            this.centerTimer = null;
        }

        protected virtual void Timer_Elapsed(object sender, EventArgs e)
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
