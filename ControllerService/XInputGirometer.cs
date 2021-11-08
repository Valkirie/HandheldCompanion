using Microsoft.Extensions.Logging;
using System;
using Windows.Devices.Sensors;

namespace ControllerService
{
    public class XInputGirometerReadingChangedEventArgs : EventArgs
    {
        public float AngularStickX { get; set; }
        public float AngularStickY { get; set; }
        public float AngularStickZ { get; set; }

        public float AngularVelocityX { get; set; }
        public float AngularVelocityY { get; set; }
        public float AngularVelocityZ { get; set; }
    }

    public class XInputGirometer
    {
        public Gyrometer sensor;
        public float multiplier = 1.0f;

        public event XInputGirometerReadingChangedEventHandler ReadingChanged;
        public delegate void XInputGirometerReadingChangedEventHandler(Object sender, XInputGirometerReadingChangedEventArgs e);

        private readonly ILogger<ControllerService> logger;

        public XInputGirometer(ILogger<ControllerService> logger)
        {
            this.logger = logger;
            sensor = Gyrometer.GetDefault();
            if (sensor != null)
            {
                sensor.ReportInterval = sensor.MinimumReportInterval;
                logger.LogInformation($"Gyrometer initialised.");
                logger.LogInformation($"Gyrometer report interval set to {sensor.ReportInterval}ms");

                sensor.ReadingChanged += GyroReadingChanged;
            }
        }

        void GyroReadingChanged(Gyrometer sender, GyrometerReadingChangedEventArgs args)
        {
            GyrometerReading reading = args.Reading;

            // raise event
            XInputGirometerReadingChangedEventArgs newargs = new XInputGirometerReadingChangedEventArgs()
            {
                AngularVelocityX = (float)reading.AngularVelocityX * multiplier,
                AngularVelocityY = (float)reading.AngularVelocityY * multiplier,
                AngularVelocityZ = (float)reading.AngularVelocityZ * multiplier
            };
            ReadingChanged?.Invoke(this, newargs);
        }
    }
}
