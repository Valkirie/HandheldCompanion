using ControllerCommon.Utils;
using Microsoft.Extensions.Logging;
using System.Numerics;
using Windows.Devices.Sensors;

namespace ControllerService.Sensors
{
    public class XInputAccelerometer : XInputSensor
    {
        public static SensorSpec sensorSpec = new SensorSpec()
        {
            minIn = -2.0f,
            maxIn = 2.0f,
            minOut = short.MinValue,
            maxOut = short.MaxValue,
        };

        private Accelerometer sensor;
        public XInputAccelerometer(int updateInterval, ILogger logger) : base(logger)
        {
            sensor = Accelerometer.GetDefault();
            if (sensor != null && ControllerService.SensorSelection == 0)
            {
                sensor.ReportInterval = (uint)updateInterval;
                logger.LogInformation("{0} initialised. Report interval set to {1}ms", this.ToString(), sensor.ReportInterval);

                sensor.ReadingChanged += ReadingChanged;
                sensor.Shaken += Shaken;
            }
            else if (ControllerService.USBGyro._serialPort.IsOpen && ControllerService.SensorSelection == 1)
            {
                ControllerService.USBGyro.ReadingChanged += ReadingChanged;
                logger.LogInformation("{0} initialised. Baud rate to {1}", this.ToString(), ControllerService.USBGyro._serialPort.BaudRate);
            }
            else
            {
                logger.LogWarning("{0} not initialised.", this.ToString());
            }
        }

        private void ReadingChanged(Vector3 AccelerationG, Vector3 AngularVelocityDeg)
        {
            this.reading.X = this.reading_fixed.X = (float)AccelerationG.X * ControllerService.handheldDevice.AngularVelocityAxis.X;
            this.reading.Y = this.reading_fixed.Y = (float)AccelerationG.Y * ControllerService.handheldDevice.AngularVelocityAxis.Y;
            this.reading.Z = this.reading_fixed.Z = (float)AccelerationG.Z * ControllerService.handheldDevice.AngularVelocityAxis.Z;

            base.ReadingChanged();
        }

        private void ReadingChanged(Accelerometer sender, AccelerometerReadingChangedEventArgs args)
        {
            this.reading.X = this.reading_fixed.X = (float)args.Reading.AccelerationX * ControllerService.handheldDevice.AccelerationAxis.X;
            this.reading.Y = this.reading_fixed.Y = (float)args.Reading.AccelerationZ * ControllerService.handheldDevice.AccelerationAxis.Z;
            this.reading.Z = this.reading_fixed.Z = (float)args.Reading.AccelerationY * ControllerService.handheldDevice.AccelerationAxis.Y;

            base.ReadingChanged();
        }

        private void Shaken(Accelerometer sender, AccelerometerShakenEventArgs args)
        {
            // throw new NotImplementedException();
        }

        public new Vector3 GetCurrentReading(bool center = false, bool ratio = false)
        {
            Vector3 reading = new Vector3()
            {
                X = center ? this.reading_fixed.X : this.reading.X,
                Y = center ? this.reading_fixed.Y : this.reading.Y,
                Z = center ? this.reading_fixed.Z : this.reading.Z
            };

            reading *= ControllerService.profile.accelerometer;

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

            return reading;
        }
    }
}