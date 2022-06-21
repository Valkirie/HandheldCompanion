using ControllerCommon.Managers;
using ControllerCommon.Sensors;
using ControllerCommon.Utils;
using System.Collections.Generic;
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

        public XInputGirometer(SensorFamily sensorFamily, int updateInterval) : base()
        {
            this.updateInterval = updateInterval;
            centerTimer.Interval = updateInterval * 6;

            UpdateSensor(sensorFamily);
        }

        public void UpdateSensor(SensorFamily sensorFamily)
        {
            switch (sensorFamily)
            {
                case SensorFamily.WindowsDevicesSensors:
                    sensor = Gyrometer.GetDefault();
                    break;
                case SensorFamily.SerialUSBIMU:
                    sensor = SerialUSBIMU.GetDefault();
                    break;
            }

            if (sensor == null)
            {
                LogManager.LogWarning("{0} not initialised as a {1}.", this.ToString(), sensorFamily.ToString());
                return;
            }

            switch (sensorFamily)
            {
                case SensorFamily.WindowsDevicesSensors:
                    ((Gyrometer)sensor).ReportInterval = (uint)updateInterval;

                    LogManager.LogInformation("{0} initialised as a {1}. Report interval set to {2}ms", this.ToString(), sensorFamily.ToString(), updateInterval);
                    break;
                case SensorFamily.SerialUSBIMU:
                    LogManager.LogInformation("{0} initialised as a {1}. Baud rate set to {2}", this.ToString(), sensorFamily.ToString(), ((SerialUSBIMU)sensor).GetInterval());
                    break;
            }

            StartListening(sensorFamily);
        }

        public void StartListening(SensorFamily sensorFamily)
        {
            switch (sensorFamily)
            {
                case SensorFamily.WindowsDevicesSensors:
                    ((Gyrometer)sensor).ReadingChanged += ReadingChanged;
                    break;
                case SensorFamily.SerialUSBIMU:
                    ((SerialUSBIMU)sensor).ReadingChanged += ReadingChanged;
                    break;
            }
        }

        public void StopListening(SensorFamily sensorFamily)
        {
            if (sensor is null)
                return;

            switch (sensorFamily)
            {
                case SensorFamily.WindowsDevicesSensors:
                    ((Gyrometer)sensor).ReadingChanged -= ReadingChanged;
                    break;
                case SensorFamily.SerialUSBIMU:
                    ((SerialUSBIMU)sensor).ReadingChanged -= ReadingChanged;
                    break;
            }
        }

        private void ReadingChanged(Vector3 AccelerationG, Vector3 AngularVelocityDeg)
        {
            this.reading.X = this.reading_fixed.X = (float)AngularVelocityDeg.X;
            this.reading.Y = this.reading_fixed.Y = (float)AngularVelocityDeg.Y;
            this.reading.Z = this.reading_fixed.Z = (float)AngularVelocityDeg.Z;

            base.ReadingChanged();
        }

        private void ReadingChanged(Gyrometer sender, GyrometerReadingChangedEventArgs args)
        {
            foreach (char axis in reading_axis.Keys)
            {
                switch (ControllerService.handheldDevice.AngularVelocityAxisSwap[axis])
                {
                    default:
                    case 'X':
                        reading_axis[axis] = args.Reading.AngularVelocityX;
                        break;
                    case 'Y':
                        reading_axis[axis] = args.Reading.AngularVelocityY;
                        break;
                    case 'Z':
                        reading_axis[axis] = args.Reading.AngularVelocityZ;
                        break;
                }
            }

            this.reading.X = this.reading_fixed.X = (float)reading_axis['X'] * ControllerService.handheldDevice.AngularVelocityAxis.X;
            this.reading.Y = this.reading_fixed.Y = (float)reading_axis['Y'] * ControllerService.handheldDevice.AngularVelocityAxis.Y;
            this.reading.Z = this.reading_fixed.Z = (float)reading_axis['Z'] * ControllerService.handheldDevice.AngularVelocityAxis.Z;

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
