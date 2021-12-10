using Microsoft.Extensions.Logging;
using System;
using System.Numerics;
using Windows.Devices.Sensors;

namespace ControllerService
{
    public class XInputGirometerReadingChangedEventArgs : EventArgs
    {
        public Vector3 AngularVelocity { get; set; }
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
            Vector3 v_reading = new Vector3((float)reading.AngularVelocityX, (float)reading.AngularVelocityY, (float)reading.AngularVelocityZ);
            v_reading *= controller.profile.gyrometer;

            // raise event
            XInputGirometerReadingChangedEventArgs newargs = new XInputGirometerReadingChangedEventArgs()
            {
                AngularVelocity = v_reading
            };
            ReadingChanged?.Invoke(this, newargs);
        }
    }
}
