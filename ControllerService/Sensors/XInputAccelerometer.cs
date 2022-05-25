using ControllerCommon.Sensors;
using ControllerCommon.Utils;
using Microsoft.Extensions.Logging;
using System.Numerics;
using Windows.Devices.Sensors;
using static ControllerCommon.Utils.DeviceUtils;

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

        public XInputAccelerometer(SensorFamily family, int updateInterval, ILogger logger) : base(logger)
        {
            switch (family)
            {
                case SensorFamily.WindowsDevicesSensors:
                    sensor = Accelerometer.GetDefault();
                    filter.SetFilterAttrs(ControllerService.handheldDevice.oneEuroSettings.minCutoff, ControllerService.handheldDevice.oneEuroSettings.beta);
                    break;
                case SensorFamily.SerialUSBIMU:
                    sensor = SerialUSBIMU.GetDefault(logger);
                    filter.SetFilterAttrs(((SerialUSBIMU)sensor).GetFilterCutoff(), ((SerialUSBIMU)sensor).GetFilterBeta());
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
                    ((Accelerometer)sensor).ReportInterval = (uint)updateInterval;
                    ((Accelerometer)sensor).ReadingChanged += ReadingChanged;

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
            this.reading.X = this.reading_fixed.X = (float)filter.axis1Filter.Filter(AccelerationG.X * ControllerService.handheldDevice.AngularVelocityAxis.X, 1 / XInputController.DeltaSeconds);
            this.reading.Y = this.reading_fixed.Y = (float)filter.axis2Filter.Filter(AccelerationG.Y * ControllerService.handheldDevice.AngularVelocityAxis.Y, 1 / XInputController.DeltaSeconds);
            this.reading.Z = this.reading_fixed.Z = (float)filter.axis3Filter.Filter(AccelerationG.Z * ControllerService.handheldDevice.AngularVelocityAxis.Z, 1 / XInputController.DeltaSeconds);

            base.ReadingChanged();
        }

        private void ReadingChanged(Accelerometer sender, AccelerometerReadingChangedEventArgs args)
        {
            this.reading.X = this.reading_fixed.X = (float)filter.axis1Filter.Filter(args.Reading.AccelerationX * ControllerService.handheldDevice.AccelerationAxis.X, 1 / XInputController.DeltaSeconds);
            this.reading.Y = this.reading_fixed.Y = (float)filter.axis2Filter.Filter(args.Reading.AccelerationZ * ControllerService.handheldDevice.AccelerationAxis.Z, 1 / XInputController.DeltaSeconds);
            this.reading.Z = this.reading_fixed.Z = (float)filter.axis3Filter.Filter(args.Reading.AccelerationY * ControllerService.handheldDevice.AccelerationAxis.Y, 1 / XInputController.DeltaSeconds);

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