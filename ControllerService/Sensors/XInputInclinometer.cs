using Microsoft.Extensions.Logging;
using System;
using System.Numerics;
using Windows.Devices.Sensors;

namespace ControllerService.Sensors
{
    public class XInputInclinometer : XInputSensor
    {
        public XInputInclinometer(int updateInterval, ILogger logger) : base(updateInterval, logger)
        {
            Accelerometer sensor = Accelerometer.GetDefault();
            if (sensor != null && ControllerService.SensorSelection == 0)
            {
                sensor.ReportInterval = (uint)updateInterval;
                logger.LogInformation("{0} initialised. Report interval set to {1}ms", this.ToString(), sensor.ReportInterval);
                sensor.ReadingChanged += ReadingChanged;
            }
            else if (ControllerService.USBGyro._serialPort.IsOpen && ControllerService.SensorSelection == 1)
            {
                ControllerService.USBGyro.ReadingChanged += USBGyro_ReadingChanged;
                logger.LogInformation("{0} initialised. Baud rate to {1}", this.ToString(), ControllerService.USBGyro._serialPort.BaudRate);
            }
            else
            {
                logger.LogWarning("{0} not initialised.", this.ToString());
            }
        }

        private void USBGyro_ReadingChanged(Vector3 AccelerationG, Vector3 AngularVelocityDeg)
        {
            this.reading.X = this.reading_fixed.X = (float)AccelerationG.X * ControllerService.handheldDevice.AngularVelocityAxis.X;
            this.reading.Y = this.reading_fixed.Y = (float)AccelerationG.Y * ControllerService.handheldDevice.AngularVelocityAxis.Y;
            this.reading.Z = this.reading_fixed.Z = (float)AccelerationG.Z * ControllerService.handheldDevice.AngularVelocityAxis.Z;

            base.ReadingChanged();
        }

        private void ReadingChanged(Accelerometer sender, AccelerometerReadingChangedEventArgs args)
        {
            AccelerometerReading reading = args.Reading;

            this.reading.X = this.reading_fixed.X = (float)reading.AccelerationX * ControllerService.handheldDevice.AccelerationAxis.X;
            this.reading.Y = this.reading_fixed.Y = (float)reading.AccelerationZ * ControllerService.handheldDevice.AccelerationAxis.Z;
            this.reading.Z = this.reading_fixed.Z = (float)reading.AccelerationY * ControllerService.handheldDevice.AccelerationAxis.Y;

            base.ReadingChanged();
        }

        public new Vector3 GetCurrentReading(bool center = false)
        {
            Vector3 reading = new Vector3()
            {
                X = center ? this.reading_fixed.X : this.reading.X,
                Y = center ? this.reading_fixed.Y : this.reading.Y,
                Z = center ? this.reading_fixed.Z : this.reading.Z
            };

            var readingZ = ControllerService.profile.steering == 0 ? reading.Z : reading.Y;
            var readingY = ControllerService.profile.steering == 0 ? reading.Y : -reading.Z;
            var readingX = ControllerService.profile.steering == 0 ? reading.X : reading.X;

            if (ControllerService.profile.inverthorizontal)
            {
                readingY *= -1.0f;
                readingZ *= -1.0f;
            }

            if (ControllerService.profile.invertvertical)
            {
                readingY *= -1.0f;
                readingX *= -1.0f;
            }

            reading.X = readingX;
            reading.Y = readingY;
            reading.Z = readingZ;

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