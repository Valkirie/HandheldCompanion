using ControllerCommon.Utils;
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

    public abstract class IMUSensor : IDisposable
    {
        protected Vector3 reading = new();
        protected Vector3 reading_fixed = new();

        protected static SensorSpec sensorSpec;

        protected Timer centerTimer;
        protected int updateInterval;
        protected SensorFamily sensorFamily;

        public object sensor;
        public OneEuroFilter3D filter = new();

        protected bool disposed;

        protected Dictionary<char, double> reading_axis = new Dictionary<char, double>()
        {
            { 'X', 0.0d },
            { 'Y', 0.0d },
            { 'Z', 0.0d },
        };

        protected IMUSensor()
        {
            this.centerTimer = new Timer(100);
            this.centerTimer.AutoReset = false;
            this.centerTimer.Elapsed += CenterTimer_Elapsed;
        }

        private void CenterTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            this.reading_fixed.X = this.reading_fixed.Y = this.reading_fixed.Z = 0;
        }

        protected virtual void ReadingChanged()
        {
            if (centerTimer is null)
                return;

            // reset reading after inactivity
            this.centerTimer.Stop();
            this.centerTimer.Start();
        }

        public virtual void StartListening()
        { }

        public virtual void StopListening()
        {
            if (centerTimer is null)
                return;

            this.centerTimer.Stop();
            this.centerTimer.Dispose();
            this.centerTimer = null;
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

        public virtual void Dispose()
        {
            // Dispose of unmanaged resources.
            Dispose(true);
            // Suppress finalization.
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposed)
            {
                if (disposing)
                {
                    StopListening();
                }
            }
            //dispose unmanaged resources
            disposed = true;
        }
    }
}
