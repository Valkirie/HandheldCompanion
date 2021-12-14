using Microsoft.Extensions.Logging;
using System;
using System.Numerics;
using Windows.Devices.Sensors;

namespace ControllerService
{
    public class XInputGirometer
    {
        public Gyrometer sensor;
        private Vector3 reading = new();

        public event XInputGirometerReadingChangedEventHandler ReadingChanged;
        public delegate void XInputGirometerReadingChangedEventHandler(Object sender, Vector3 e);

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

            this.reading.X = (float)-reading.AngularVelocityX;
            this.reading.Y = (float)reading.AngularVelocityZ;
            this.reading.Z = (float)reading.AngularVelocityY;

            this.reading *= controller.profile.gyrometer;

            // raise event
            ReadingChanged?.Invoke(this, this.reading);
        }
    }
}
