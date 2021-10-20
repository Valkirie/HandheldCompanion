using System;
using System.Diagnostics;
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

        public XInputAccelerometer(EventLog eventLog1)
        {
            sensor = Accelerometer.GetDefault();
            if (sensor != null)
            {
                sensor.ReportInterval = sensor.MinimumReportInterval;
                eventLog1.WriteEntry($"Accelerometer initialised.");
                eventLog1.WriteEntry($"Accelerometer report interval set to {sensor.ReportInterval}ms");

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
