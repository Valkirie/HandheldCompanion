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

        public event XInputGirometerReadingChangedEventHandler ReadingChanged;
        public delegate void XInputGirometerReadingChangedEventHandler(Object sender, XInputGirometerReadingChangedEventArgs e);

        private readonly ILogger logger;
        private readonly XInputController controller;

        public XInputGirometer(XInputController controller, ILogger logger)
        {
            this.logger = logger;
            this.controller = controller;

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

            double AngularVelocityX = controller.profile.steering == 0 ? reading.AngularVelocityX : reading.AngularVelocityZ;   // gyroPitchFull
            double AngularVelocityY = controller.profile.steering == 0 ? reading.AngularVelocityY : reading.AngularVelocityY;   // gyroYawFull
            double AngularVelocityZ = controller.profile.steering == 0 ? reading.AngularVelocityZ : reading.AngularVelocityX;   // gyroRollFull

            AngularVelocityX = (controller.profile.inverthorizontal ? -1 : 1) * AngularVelocityX;
            AngularVelocityY = (controller.profile.invertvertical ? -1 : 1) * AngularVelocityY;

            // raise event
            XInputGirometerReadingChangedEventArgs newargs = new XInputGirometerReadingChangedEventArgs()
            {
                AngularVelocityX = (float)AngularVelocityX * controller.profile.gyrometer,
                AngularVelocityY = (float)AngularVelocityY * controller.profile.gyrometer,
                AngularVelocityZ = (float)AngularVelocityZ * controller.profile.gyrometer
            };
            ReadingChanged?.Invoke(this, newargs);
        }
    }
}
