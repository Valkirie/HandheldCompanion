using ControllerCommon;
using ControllerCommon.Managers;
using ControllerCommon.Sensors;
using ControllerCommon.Utils;
using System.Numerics;
using Windows.Devices.Sensors;
using static ControllerCommon.Utils.DeviceUtils;

namespace ControllerService.Sensors
{
    public class IMUGyrometer : IMUSensor
    {
        public static new SensorSpec sensorSpec = new()
        {
            minIn = -128.0f,
            maxIn = 128.0f,
            minOut = -2048.0f,
            maxOut = 2048.0f,
        };

        public IMUGyrometer(SensorFamily sensorFamily, int updateInterval) : base()
        {
            this.sensorFamily = sensorFamily;
            this.updateInterval = updateInterval;
            base.centerTimer.Interval = updateInterval * 6;

            UpdateSensor();
        }

        public void UpdateSensor()
        {
            switch (sensorFamily)
            {
                case SensorFamily.Windows:
                    sensor = Gyrometer.GetDefault();
                    break;
                case SensorFamily.SerialUSBIMU:
                    sensor = SerialUSBIMU.GetDefault();
                    break;
                case SensorFamily.Controller:
                    sensor = new object();
                    break;
            }

            if (sensor is null)
            {
                LogManager.LogWarning("{0} not initialised as a {1}", this.ToString(), sensorFamily.ToString());
                return;
            }

            switch (sensorFamily)
            {
                case SensorFamily.Windows:
                    {
                        // workaround Bosch BMI323
                        string path = ((Gyrometer)sensor).DeviceId;
                        using (PnPDetails details = DeviceManager.GetDeviceByInterfaceId(path))
                            details.CyclePort();

                        ((Gyrometer)sensor).ReportInterval = (uint)updateInterval;

                        LogManager.LogInformation("{0} initialised as a {1}. Report interval set to {2}ms", this.ToString(), sensorFamily.ToString(), updateInterval);
                    }
                    break;
                case SensorFamily.SerialUSBIMU:
                    LogManager.LogInformation("{0} initialised as a {1}. Baud rate set to {2}", this.ToString(), sensorFamily.ToString(), ((SerialUSBIMU)sensor).GetInterval());
                    break;
                case SensorFamily.Controller:
                    LogManager.LogInformation("{0} initialised as a {1}", this.ToString(), sensorFamily.ToString());
                    break;
            }

            StartListening();
        }

        public override void StartListening()
        {
            switch (sensorFamily)
            {
                case SensorFamily.Windows:
                    ((Gyrometer)sensor).ReadingChanged += ReadingChanged;
                    break;
                case SensorFamily.SerialUSBIMU:
                    ((SerialUSBIMU)sensor).ReadingChanged += ReadingChanged;
                    break;
            }
        }

        public override void StopListening()
        {
            if (sensor is null)
                return;

            switch (sensorFamily)
            {
                case SensorFamily.Windows:
                    ((Gyrometer)sensor).ReadingChanged -= ReadingChanged;
                    break;
                case SensorFamily.SerialUSBIMU:
                    ((SerialUSBIMU)sensor).ReadingChanged -= ReadingChanged;
                    break;
            }

            sensor = null;

            base.StopListening();
        }

        public void ReadingChanged(float GyroRoll, float GyroPitch, float GyroYaw)
        {
            switch (sensorFamily)
            {
                case SensorFamily.Controller:
                    {
                        this.reading.X = this.reading_fixed.X = GyroRoll;
                        this.reading.Y = this.reading_fixed.Y = GyroPitch;
                        this.reading.Z = this.reading_fixed.Z = GyroYaw;

                        base.ReadingChanged();
                    }
                    break;
            }
        }

        private void ReadingChanged(Vector3 AccelerationG, Vector3 AngularVelocityDeg)
        {
            /*
            this.reading.X = this.reading_fixed.X = (float)filter.axis1Filter.Filter(AccelerationG.X, IMU.DeltaSeconds);
            this.reading.Y = this.reading_fixed.Y = (float)filter.axis2Filter.Filter(AccelerationG.Y, IMU.DeltaSeconds);
            this.reading.Z = this.reading_fixed.Z = (float)filter.axis3Filter.Filter(AccelerationG.Z, IMU.DeltaSeconds);
            */
            this.reading.X = this.reading_fixed.X = (float)AngularVelocityDeg.X;
            this.reading.Y = this.reading_fixed.Y = (float)AngularVelocityDeg.Y;
            this.reading.Z = this.reading_fixed.Z = (float)AngularVelocityDeg.Z;

            base.ReadingChanged();
        }

        private void ReadingChanged(Gyrometer sender, GyrometerReadingChangedEventArgs args)
        {
            if (sensor is null)
                return;

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

            reading *= ControllerService.currentProfile.GyrometerMultiplier;

            var readingZ = ControllerService.currentProfile.SteeringAxis == 0 ? reading.Z : reading.Y;
            var readingY = ControllerService.currentProfile.SteeringAxis == 0 ? reading.Y : reading.Z;
            var readingX = ControllerService.currentProfile.SteeringAxis == 0 ? reading.X : reading.X;

            if (ControllerService.currentProfile.MotionInvertHorizontal)
            {
                readingY *= -1.0f;
                readingZ *= -1.0f;
            }

            if (ControllerService.currentProfile.MotionInvertVertical)
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
