using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Numerics;
using Windows.Devices.Sensors;
using static ControllerCommon.Utils;

namespace ControllerService.Sensors
{
    public class XInputGirometer : XInputSensor
    {
        public Gyrometer sensor;

        public static new float MaxValue = 128.0f;

        private long prev_microseconds;
        private readonly OneEuroFilter3D gyroFilter;

        public event ReadingChangedEventHandler ReadingChanged;
        public delegate void ReadingChangedEventHandler(XInputGirometer sender, Vector3 e);

        private readonly ILogger logger;

        public XInputGirometer(XInputController controller, ILogger logger) : base(controller)
        {
            this.logger = logger;

            gyroFilter = new OneEuroFilter3D();

            sensor = Gyrometer.GetDefault();
            if (sensor != null)
            {
                sensor.ReportInterval = sensor.MinimumReportInterval;
                logger.LogInformation("{0} initialised. Report interval set to {1}ms", this.ToString(), sensor.ReportInterval);

                sensor.ReadingChanged += ReadingHasChanged;
            }
        }

        public override string ToString()
        {
            return this.GetType().Name;
        }

        private void ReadingHasChanged(Gyrometer sender, GyrometerReadingChangedEventArgs args)
        {
            GyrometerReading reading = args.Reading;

            var microseconds = (long)(reading.Timestamp.Ticks / (Stopwatch.Frequency / (1000L * 1000L)));
            var elapsedTime = 0.000001 * (microseconds - prev_microseconds);
            var rate = elapsedTime;

            prev_microseconds = microseconds;

            float readingX = this.reading.X = (float)gyroFilter.axis1Filter.Filter(reading.AngularVelocityX, rate);
            float readingY = this.reading.Y = (float)gyroFilter.axis1Filter.Filter(reading.AngularVelocityZ, rate);
            float readingZ = this.reading.Z = (float)gyroFilter.axis1Filter.Filter(reading.AngularVelocityY, rate);

            if (controller.virtualTarget != null)
            {
                this.reading *= controller.profile.gyrometer;

                this.reading.Y *= controller.WidhtHeightRatio;

                this.reading.Z = controller.profile.steering == 0 ? readingZ : readingY;
                this.reading.Y = controller.profile.steering == 0 ? readingY : readingZ;
                this.reading.X = controller.profile.steering == 0 ? readingX : readingX;

                if (controller.profile.inverthorizontal)
                {
                    this.reading.Y *= -1.0f;
                    this.reading.Z *= -1.0f;
                }

                if (controller.profile.invertvertical)
                {
                    this.reading.Y *= -1.0f;
                    this.reading.X *= -1.0f;
                }
            }

            logger?.LogDebug("XInputGirometer.ReadingChanged({0:00.####}, {1:00.####}, {2:00.####})", this.reading.X, this.reading.Y, this.reading.Z);

            // raise event
            ReadingChanged?.Invoke(this, this.reading);
        }
    }
}
