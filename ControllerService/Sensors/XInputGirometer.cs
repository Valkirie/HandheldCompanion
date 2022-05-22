using ControllerCommon;
using ControllerCommon.Utils;
using Microsoft.Extensions.Logging;
using System.Numerics;
using Windows.Devices.Sensors;
using static ControllerCommon.Utils.DeviceUtils;

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

        public XInputGirometer(SensorFamily family, int updateInterval, ILogger logger) : base(logger)
        {
            centerTimer.Interval = updateInterval * 6;

            switch(family)
            {
                case SensorFamily.WindowsDevicesSensors:
                    sensor = Gyrometer.GetDefault();
                    break;
                case SensorFamily.SerialUSBIMU:
                    sensor = SerialUSBIMU.GetDefault(logger);
                    break;
            }

            if (sensor == null)
            {
                logger.LogWarning("{0}:{1} not initialised.", this.ToString(), family.ToString());
                return;
            }

            switch (family)
            {
                case SensorFamily.WindowsDevicesSensors:
                    ((Gyrometer)sensor).ReportInterval = (uint)updateInterval;
                    ((Gyrometer)sensor).ReadingChanged += ReadingChanged;

                    logger.LogInformation("{0}:{1} initialised. Report interval set to {2}ms", this.ToString(), family.ToString(), updateInterval);
                    break;
                case SensorFamily.SerialUSBIMU:
                    ((SerialUSBIMU)sensor).ReadingChanged += ReadingChanged;

                    logger.LogInformation("{0}:{1} initialised. Report interval set to {2}", this.ToString(), family.ToString(), ((SerialUSBIMU)sensor).GetInterval());
                    break;
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
