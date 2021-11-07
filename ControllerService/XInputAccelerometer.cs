using System;
using Windows.Devices.Sensors;
using Microsoft.Extensions.Logging;

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

        public event XInputAccelerometerReadingChangedEventHandler ReadingChanged;
        public delegate void XInputAccelerometerReadingChangedEventHandler(Object sender, XInputAccelerometerReadingChangedEventArgs e);

        private readonly ILogger<ControllerService> logger;

        public XInputAccelerometer( ILogger<ControllerService> logger)
        {
            this.logger = logger;

            sensor = Accelerometer.GetDefault();
            if (sensor != null)
            {
                sensor.ReportInterval = sensor.MinimumReportInterval;
                logger.LogInformation($"Accelerometer initialised.");
                logger.LogInformation($"Accelerometer report interval set to {sensor.ReportInterval}ms");

                sensor.ReadingChanged += AcceleroReadingChanged;
            }
        }

        void AcceleroReadingChanged(Accelerometer sender, AccelerometerReadingChangedEventArgs args)
        {
            AccelerometerReading reading = args.Reading;

            // raise event
            XInputAccelerometerReadingChangedEventArgs newargs = new XInputAccelerometerReadingChangedEventArgs()
            {
                AccelerationX = (float)reading.AccelerationX,
                AccelerationY = (float)reading.AccelerationY,
                AccelerationZ = (float)reading.AccelerationZ
            };
            ReadingChanged?.Invoke(this, newargs);
        }
    }
}
