using ControllerCommon.Utils;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;
using System.Timers;

namespace ControllerService.Sensors
{
    [Flags]
    public enum XInputSensorFlags
    {
        Default         =   0000,
        RawValue        =   0001,
        Centered        =   0010,
        WithRatio       =   0100,
        CenteredRaw     =   RawValue | Centered,
        CenteredRatio   =   RawValue | WithRatio,
    }

    public abstract class XInputSensor
    {
        protected Vector3 reading = new();
        protected Vector3 reading_fixed = new();

        protected static SensorSpec sensorSpec;

        protected Timer centerTimer;

        protected readonly ILogger logger;

        protected XInputSensor(ILogger logger)
        {
            this.centerTimer = new Timer() { Enabled = false, AutoReset = false, Interval = 100 };
            this.centerTimer.Elapsed += Timer_Elapsed;
        }

        protected virtual void ReadingChanged()
        {
            // reset reading after inactivity
            centerTimer.Stop();
            centerTimer.Start();

            Task.Run(() => logger?.LogDebug("{0}.ReadingChanged({1:00.####}, {2:00.####}, {3:00.####})", this.GetType().Name, this.reading.X, this.reading.Y, this.reading.Z));
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
