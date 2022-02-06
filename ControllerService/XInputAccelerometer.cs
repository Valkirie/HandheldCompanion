using Microsoft.Extensions.Logging;
using System;
using System.Diagnostics;
using System.Numerics;
using Windows.Devices.Sensors;
using static ControllerCommon.Utils;

namespace ControllerService
{
    public class XInputAccelerometer
    {
        public Accelerometer sensor;
        private Vector3 reading = new();

        private long prev_microseconds;
        private readonly OneEuroFilter3D accelFilter;

        public event ReadingChangedEventHandler ReadingHasChanged;
        public delegate void ReadingChangedEventHandler(XInputAccelerometer sender, Vector3 e);

        private readonly ILogger logger;
        private readonly XInputController xinput;

        public XInputAccelerometer(XInputController controller, ILogger logger)
        {
            this.logger = logger;
            this.xinput = controller;

            accelFilter = new OneEuroFilter3D();

            sensor = Accelerometer.GetDefault();
            if (sensor != null)
            {
                sensor.ReportInterval = sensor.MinimumReportInterval;
                logger.LogInformation("{0} initialised. Report interval set to {1}ms", this.ToString(), sensor.ReportInterval);

                sensor.ReadingChanged += ReadingChanged;
                sensor.Shaken += Shaken;
            }
            else
            {
                logger.LogInformation("{0} not initialised.", this.ToString());
            }
        }

        private void Shaken(Accelerometer sender, AccelerometerShakenEventArgs args)
        {
            return; // implement me
            throw new NotImplementedException();
        }

        public override string ToString()
        {
            return this.GetType().Name;
        }

        private void ReadingChanged(Accelerometer sender, AccelerometerReadingChangedEventArgs args)
        {
            AccelerometerReading reading = args.Reading;

            var microseconds = (long)(reading.Timestamp.Ticks / (Stopwatch.Frequency / (1000L * 1000L)));
            var elapsedTime = 0.000001 * (microseconds - prev_microseconds);
            var rate = elapsedTime;

            prev_microseconds = microseconds;

            float readingX = this.reading.X = (float)accelFilter.axis1Filter.Filter(reading.AccelerationX, rate);
            float readingY = this.reading.Y = (float)accelFilter.axis1Filter.Filter(reading.AccelerationZ, rate);
            float readingZ = this.reading.Z = (float)accelFilter.axis1Filter.Filter(reading.AccelerationY, rate);

            if (xinput.virtualTarget != null)
            {
                this.reading *= xinput.profile.accelerometer;

                this.reading.Z = xinput.profile.steering == 0 ? readingZ : readingY;
                this.reading.Y = xinput.profile.steering == 0 ? readingY : -readingZ;
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
            ReadingHasChanged?.Invoke(this, this.reading);
        }
    }
}
