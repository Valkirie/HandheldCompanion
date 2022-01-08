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

        public event XInputAccelerometerReadingChangedEventHandler ReadingChanged;
        public delegate void XInputAccelerometerReadingChangedEventHandler(XInputAccelerometer sender, Vector3 e);

        private readonly ILogger logger;
        private readonly XInputController controller;

        public XInputAccelerometer(XInputController controller, ILogger logger)
        {
            this.logger = logger;
            this.controller = controller;

            accelFilter = new OneEuroFilter3D();

            sensor = Accelerometer.GetDefault();
            if (sensor != null)
            {
                sensor.ReportInterval = sensor.MinimumReportInterval;
                logger.LogInformation("{0} initialised. Report interval set to {1}ms", this.ToString(), sensor.ReportInterval);

                sensor.ReadingChanged += AcceleroReadingChanged;
            }
        }

        public override string ToString()
        {
            return this.GetType().Name;
        }

        void AcceleroReadingChanged(Accelerometer sender, AccelerometerReadingChangedEventArgs args)
        {
            AccelerometerReading reading = args.Reading;

            var microseconds = (long)(reading.Timestamp.Ticks / (Stopwatch.Frequency / (1000L * 1000L)));
            var elapsedTime = 0.000001 * (microseconds - prev_microseconds);
            var rate = elapsedTime;

            prev_microseconds = microseconds;

            float readingX = this.reading.X = (float)accelFilter.axis1Filter.Filter(reading.AccelerationX, rate);
            float readingY = this.reading.Y = (float)accelFilter.axis1Filter.Filter(reading.AccelerationZ, rate);
            float readingZ = this.reading.Z = (float)accelFilter.axis1Filter.Filter(reading.AccelerationY, rate);

            if (controller.Target != null)
            {
                this.reading *= controller.Target.Profile.accelerometer;

                this.reading.Z = controller.Target.Profile.steering == 0 ? readingZ : readingY;
                this.reading.Y = controller.Target.Profile.steering == 0 ? readingY : -readingZ;
                this.reading.X = controller.Target.Profile.steering == 0 ? readingX : readingX;

                if (controller.Target.Profile.inverthorizontal)
                {
                    this.reading.Y *= -1.0f;
                    this.reading.Z *= -1.0f;
                }

                if (controller.Target.Profile.invertvertical)
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
