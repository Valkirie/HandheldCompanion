using Microsoft.Extensions.Logging;
using System;
using System.Numerics;
using Windows.Devices.Sensors;

namespace ControllerService
{
    public class XInputGirometer
    {
        public Gyrometer sensor;

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

        private Vector3 v_reading = new();
        void GyroReadingChanged(Gyrometer sender, GyrometerReadingChangedEventArgs args)
        {
            GyrometerReading reading = args.Reading;

            v_reading.X = (float)-reading.AngularVelocityX;
            v_reading.Y = (float)reading.AngularVelocityZ;
            v_reading.Z = (float)reading.AngularVelocityY;

            v_reading *= controller.profile.gyrometer;

            // raise event
            ReadingChanged?.Invoke(this, v_reading);
        }
    }
}
