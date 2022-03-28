using ControllerCommon.Utils;
using Microsoft.Extensions.Logging;
using System.Numerics;
using System.Threading.Tasks;
using System.Timers;

namespace ControllerService.Sensors
{
    public abstract class XInputSensor
    {
        protected Vector3 reading = new();
        protected Vector3 reading_fixed = new();

        protected XInputController controller;

        protected static SensorSpec sensorSpec;

        protected Timer updateTimer;
        protected int updateInterval;

        protected readonly ILogger logger;

        public event ReadingChangedEventHandler ReadingHasChanged;
        public delegate void ReadingChangedEventHandler(XInputSensor sender, Vector3 e);

        protected XInputSensor(XInputController controller, ILogger logger)
        {
            this.controller = controller;

            this.updateInterval = controller.updateInterval;

            this.updateTimer = new Timer() { Enabled = false, AutoReset = false, Interval = 100 };
            this.updateTimer.Elapsed += Timer_Elapsed;
        }

        protected virtual void ReadingChanged()
        {
            // reset reading after inactivity
            updateTimer.Stop();
            updateTimer.Start();

            // raise event
            ReadingHasChanged?.Invoke(this, this.reading);

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

        protected virtual Vector3 GetCurrentReading(bool center = false)
        {
            return center ? this.reading_fixed : this.reading;
        }

        public Vector3 GetCurrentReadingRaw(bool center = false)
        {
            return center ? this.reading_fixed : this.reading;
        }
    }
}
