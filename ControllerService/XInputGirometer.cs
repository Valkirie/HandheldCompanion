using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Numerics;
using Windows.Devices.Sensors;
using static ControllerCommon.Utils;

namespace ControllerService
{
    public class XInputGirometer
    {
        public Gyrometer sensor;

        private Vector3 reading = new();

        private long prev_microseconds;
        private readonly OneEuroFilter3D gyroFilter;

        public event ReadingChangedEventHandler ReadingChanged;
        public delegate void ReadingChangedEventHandler(XInputGirometer sender, Vector3 e);

        private readonly ILogger logger;
        private readonly XInputController xinput;

        public XInputGirometer(XInputController controller, ILogger logger)
        {
            this.logger = logger;
            this.xinput = controller;

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

            if (xinput.virtualTarget != null)
            {
                this.reading *= xinput.profile.gyrometer;

                this.reading.Z = xinput.profile.steering == 0 ? readingZ : readingY;
                this.reading.Y = xinput.profile.steering == 0 ? readingY : readingZ;
                this.reading.X = xinput.profile.steering == 0 ? readingX : readingX;

                if (xinput.profile.inverthorizontal)
                {
                    this.reading.Y *= -1.0f;
                    this.reading.Z *= -1.0f;
                }

                if (xinput.profile.invertvertical)
                {
                    this.reading.Y *= -1.0f;
                    this.reading.X *= -1.0f;
                }
            }

            // raise event
            ReadingChanged?.Invoke(this, this.reading);
        }
    }
}
