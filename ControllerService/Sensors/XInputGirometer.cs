using ControllerCommon.Utils;
using Microsoft.Extensions.Logging;
using System.Numerics;
using Windows.Devices.Sensors;

namespace ControllerService.Sensors
{
    public class XInputGirometer : XInputSensor
    {
        public static SensorSpec sensorSpec = new SensorSpec()
        {
            minIn = -128.0f,
            maxIn = 128.0f,
            minOut = -2048.0f,
            maxOut = 2048.0f,
        };

        private Gyrometer sensor;
        public XInputGirometer(int updateInterval, ILogger logger) : base(logger)
        {
            centerTimer.Interval = updateInterval * 6;

            sensor = Gyrometer.GetDefault();
            if (sensor != null && ControllerService.SensorSelection == 0)
            {
                sensor.ReportInterval = (uint)updateInterval;
                logger.LogInformation("{0} initialised. Report interval set to {1}ms", this.ToString(), sensor.ReportInterval);

                sensor.ReadingChanged += ReadingChanged;
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
            this.reading.X = this.reading_fixed.X = (float)AngularVelocityDeg.X * ControllerService.handheldDevice.AngularVelocityAxis.X;
            this.reading.Y = this.reading_fixed.Y = (float)AngularVelocityDeg.Y * ControllerService.handheldDevice.AngularVelocityAxis.Y;
            this.reading.Z = this.reading_fixed.Z = (float)AngularVelocityDeg.Z * ControllerService.handheldDevice.AngularVelocityAxis.Z;

            base.ReadingChanged();
        }

        private void ReadingChanged(Gyrometer sender, GyrometerReadingChangedEventArgs args)
        {
            // swapping Y and Z
            this.reading.X = this.reading_fixed.X = (float)args.Reading.AngularVelocityX * ControllerService.handheldDevice.AngularVelocityAxis.X;
            this.reading.Y = this.reading_fixed.Y = (float)args.Reading.AngularVelocityZ * ControllerService.handheldDevice.AngularVelocityAxis.Z;
            this.reading.Z = this.reading_fixed.Z = (float)args.Reading.AngularVelocityY * ControllerService.handheldDevice.AngularVelocityAxis.Y;

            base.ReadingChanged();
        }

        public new Vector3 GetCurrentReading(bool center = false, bool ratio = false)
        {
            Vector3 reading = new Vector3()
            {
                X = center ? this.reading_fixed.X : this.reading.X,
                Y = center ? this.reading_fixed.Y : this.reading.Y,
                Z = center ? this.reading_fixed.Z : this.reading.Z
            };

            reading *= ControllerService.profile.gyrometer;

            if (ratio)
                reading.Y *= ControllerService.handheldDevice.WidthHeightRatio;

            var readingZ = ControllerService.profile.steering == 0 ? reading.Z : reading.Y;
            var readingY = ControllerService.profile.steering == 0 ? reading.Y : reading.Z;
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
