using Microsoft.Extensions.Logging;
using System;
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

        public event XInputGirometerReadingChangedEventHandler ReadingChanged;
        public delegate void XInputGirometerReadingChangedEventHandler(Object sender, Vector3 e);

        private readonly ILogger logger;
        private readonly XInputController controller;

        public XInputGirometer(XInputController controller, ILogger logger)
        {
            this.logger = logger;
            this.controller = controller;

            gyroFilter = new OneEuroFilter3D();

            sensor = Gyrometer.GetDefault();
            if (sensor != null)
            {
                sensor.ReportInterval = sensor.MinimumReportInterval;
                logger.LogInformation("Gyrometer initialised");
                logger.LogInformation("Gyrometer report interval set to {0}ms", sensor.ReportInterval);

                sensor.ReadingChanged += GyroReadingChanged;
            }
        }

        void GyroReadingChanged(Gyrometer sender, GyrometerReadingChangedEventArgs args)
        {
            GyrometerReading reading = args.Reading;

            var microseconds = (long)(reading.Timestamp.Ticks / (Stopwatch.Frequency / (1000L * 1000L)));
            var elapsedTime = 0.000001 * (microseconds - prev_microseconds);
            var rate = elapsedTime;

            prev_microseconds = microseconds;

            this.reading.X = (float)gyroFilter.axis1Filter.Filter(reading.AngularVelocityX, rate);
            this.reading.Y = (float)gyroFilter.axis1Filter.Filter(reading.AngularVelocityZ, rate);
            this.reading.Z = (float)gyroFilter.axis1Filter.Filter(reading.AngularVelocityY, rate);

            if (controller.target != null)
            {
                this.reading *= controller.target.profile.gyrometer;

                this.reading.Z = (controller.target.profile.inverthorizontal ? -1.0f : 1.0f) * this.reading.Z;
                this.reading.X = (controller.target.profile.invertvertical ? -1.0f : 1.0f) * this.reading.X;
            }

            // raise event
            ReadingChanged?.Invoke(this, this.reading);
        }
    }
}
