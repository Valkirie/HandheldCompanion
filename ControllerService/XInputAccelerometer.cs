using Microsoft.Extensions.Logging;
using System;
using System.Numerics;
using Windows.Devices.Sensors;

namespace ControllerService
{
    public class XInputAccelerometer
    {
        public Accelerometer sensor;
        private Vector3 reading = new();

        public event XInputAccelerometerReadingChangedEventHandler ReadingChanged;
        public delegate void XInputAccelerometerReadingChangedEventHandler(Object sender, Vector3 e);

        private readonly ILogger logger;
        private readonly XInputController controller;

        public XInputAccelerometer(XInputController controller, ILogger logger)
        {
            this.logger = logger;
            this.controller = controller;

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

            this.reading.X = (float)-reading.AccelerationX;
            this.reading.Y = (float)reading.AccelerationZ;
            this.reading.Z = (float)reading.AccelerationY;

            this.reading *= controller.profile.accelerometer;

            // raise event
            ReadingChanged?.Invoke(this, this.reading);
        }
    }
}
