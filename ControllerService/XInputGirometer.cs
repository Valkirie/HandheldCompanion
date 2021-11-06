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

        public XInputGirometer(EventLog CurrentLog)
        {
            sensor = Gyrometer.GetDefault();
            if (sensor != null)
            {
                sensor.ReportInterval = sensor.MinimumReportInterval;
                CurrentLog.WriteEntry($"Gyrometer initialised.");
                CurrentLog.WriteEntry($"Gyrometer report interval set to {sensor.ReportInterval}ms");

                sensor.ReadingChanged += GyroReadingChanged;
            }
        }

        void GyroReadingChanged(Gyrometer sender, GyrometerReadingChangedEventArgs args)
        {
            GyrometerReading reading = args.Reading;

            // raise event
            XInputGirometerReadingChangedEventArgs newargs = new XInputGirometerReadingChangedEventArgs()
            {
                AngularVelocityX = (float)reading.AngularVelocityX,
                AngularVelocityY = (float)reading.AngularVelocityY,
                AngularVelocityZ = (float)reading.AngularVelocityZ
            };
            ReadingChanged?.Invoke(this, newargs);
        }
    }
}
