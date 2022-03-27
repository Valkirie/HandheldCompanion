using Microsoft.Extensions.Logging;
using System;
using System.Numerics;
using System.Threading.Tasks;
using Windows.Devices.Sensors;

namespace ControllerService.Sensors
{
    public class XInputInclinometer : XInputSensor
    {
        public Accelerometer sensor;

        public event ReadingChangedEventHandler ReadingHasChanged;
        public delegate void ReadingChangedEventHandler(XInputInclinometer sender, Vector3 e);

        private readonly ILogger logger;

        public XInputInclinometer(XInputController controller, ILogger logger) : base(controller)
        {
            this.logger = logger;

            sensor = Accelerometer.GetDefault();
            if (sensor != null)
            {
                sensor.ReportInterval = (uint)updateInterval;
                logger.LogInformation("{0} initialised. Report interval set to {1}ms", this.ToString(), sensor.ReportInterval);
                sensor.ReadingChanged += ReadingChanged;
            }
            else
            {
                logger.LogWarning("{0} not initialised.", this.ToString());
            }
        }

        private void ReadingChanged(Accelerometer sender, AccelerometerReadingChangedEventArgs args)
        {
            AccelerometerReading reading = args.Reading;

            this.reading.X = (float)reading.AccelerationX;
            this.reading.Y = (float)reading.AccelerationZ;
            this.reading.Z = (float)reading.AccelerationY;

            Task.Run(() => logger?.LogDebug("XInputInclinometer.ReadingChanged({0:00.####}, {1:00.####}, {2:00.####})", this.reading.X, this.reading.Y, this.reading.Z));
        }

        public new Vector3 GetCurrentReading(bool center = false)
        {
            Vector3 reading = new Vector3()
            {
                X = center ? this.reading_fixed.X : this.reading.X,
                Y = center ? this.reading_fixed.Y : this.reading.Y,
                Z = center ? this.reading_fixed.Z : this.reading.Z
            };

            if (controller.virtualTarget != null)
            {
                var readingZ = controller.profile.steering == 0 ? reading.Z : reading.Y;
                var readingY = controller.profile.steering == 0 ? reading.Y : -reading.Z;
                var readingX = controller.profile.steering == 0 ? reading.X : reading.X;

                if (controller.profile.inverthorizontal)
                {
                    readingY *= -1.0f;
                    readingZ *= -1.0f;
                }

                if (controller.profile.invertvertical)
                {
                    readingY *= -1.0f;
                    readingX *= -1.0f;
                }

                reading.X = readingX;
                reading.Y = readingY;
                reading.Z = readingZ;
            }

            // Calculate angles around Y and X axis (Theta and Psi) using all 3 directions of accelerometer
            // Based on: https://www.digikey.com/en/articles/using-an-accelerometer-for-inclination-sensing               
            double angle_x_psi = -1 * (Math.Atan(reading.Y / (Math.Sqrt(Math.Pow(reading.X, 2) + Math.Pow(reading.Z, 2))))) * 180 / Math.PI;
            double angle_y_theta = -1 * (Math.Atan(reading.X / (Math.Sqrt(Math.Pow(reading.Y, 2) + Math.Pow(reading.Z, 2))))) * 180 / Math.PI;

            reading.X = (float)(angle_x_psi);
            reading.Y = (float)(angle_y_theta);

            return reading;
        }
    }
}