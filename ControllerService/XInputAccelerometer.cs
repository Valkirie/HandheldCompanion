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

        public event XInputAccelerometerReadingChangedEventHandler ReadingChanged;
        public delegate void XInputAccelerometerReadingChangedEventHandler(Object sender, XInputAccelerometerReadingChangedEventArgs e);

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

            double AccelerationX = controller.profile.steering == 0 ? reading.AccelerationX : reading.AccelerationZ;
            double AccelerationY = controller.profile.steering == 0 ? reading.AccelerationY : reading.AccelerationY;
            double AccelerationZ = controller.profile.steering == 0 ? reading.AccelerationZ : reading.AccelerationX;

            AccelerationX = (controller.profile.inverthorizontal ? -1 : 1) * AccelerationX;
            AccelerationY = (controller.profile.invertvertical ? -1 : 1) * AccelerationY;

            // raise event
            XInputAccelerometerReadingChangedEventArgs newargs = new XInputAccelerometerReadingChangedEventArgs()
            {
                AccelerationX = (float)AccelerationX * controller.profile.accelerometer,
                AccelerationY = (float)AccelerationY * controller.profile.accelerometer,
                AccelerationZ = (float)AccelerationZ * controller.profile.accelerometer
            };
            ReadingChanged?.Invoke(this, newargs);
        }
    }
}
