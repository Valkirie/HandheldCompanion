using Microsoft.Extensions.Logging;
using System;
using Windows.Devices.Sensors;

namespace ControllerService
{
    public class XInputAccelerometerReadingChangedEventArgs : EventArgs
    {
        public float AccelerationX { get; set; }
        public float AccelerationY { get; set; }
        public float AccelerationZ { get; set; }
    }

    public class XInputAccelerometer
    {
        public Accelerometer sensor;
        public float multiplier = 1.0f;

        public event XInputAccelerometerReadingChangedEventHandler ReadingChanged;
        public delegate void XInputAccelerometerReadingChangedEventHandler(Object sender, XInputAccelerometerReadingChangedEventArgs e);

        private readonly ILogger<ControllerService> logger;

        public XInputAccelerometer(ILogger<ControllerService> logger)
        {
            this.logger = logger;

            sensor = Accelerometer.GetDefault();
            if (sensor != null)
            {
                sensor.ReportInterval = sensor.MinimumReportInterval;
                logger.LogInformation("Accelerometer initialised");
                logger.LogInformation("Accelerometer report interval set to {0}ms", sensor.ReportInterval);

                sensor.ReadingChanged += AcceleroReadingChanged;
            }
        }

        void AcceleroReadingChanged(Accelerometer sender, AccelerometerReadingChangedEventArgs args)
        {
            AccelerometerReading reading = args.Reading;

            // raise event
            XInputAccelerometerReadingChangedEventArgs newargs = new XInputAccelerometerReadingChangedEventArgs()
            {
                AccelerationX = (float)reading.AccelerationX * multiplier,
                AccelerationY = (float)reading.AccelerationY * multiplier,
                AccelerationZ = (float)reading.AccelerationZ * multiplier
            };
            ReadingChanged?.Invoke(this, newargs);
        }
    }
}
