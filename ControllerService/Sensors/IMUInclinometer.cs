using ControllerCommon.Managers;
using ControllerCommon.Sensors;
using ControllerCommon.Utils;
using System;
using System.Numerics;
using Windows.Devices.Sensors;
using static ControllerCommon.Utils.DeviceUtils;

namespace ControllerService.Sensors
{
    public class IMUInclinometer : IMUSensor
    {
        public static new SensorSpec sensorSpec = new()
        {
            minIn = -2.0f,
            maxIn = 2.0f,
            minOut = short.MinValue,
            maxOut = short.MaxValue,
        };

        public IMUInclinometer(SensorFamily sensorFamily, int updateInterval) : base()
        {
            this.sensorFamily = sensorFamily;
            this.updateInterval = updateInterval;

            UpdateSensor();
        }

        public void UpdateSensor()
        {
            switch (sensorFamily)
            {
                case SensorFamily.Windows:
                    sensor = Accelerometer.GetDefault();
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
                    ((Accelerometer)sensor).ReportInterval = (uint)updateInterval;
                    filter.SetFilterAttrs(ControllerService.CurrentDevice.oneEuroSettings.minCutoff, ControllerService.CurrentDevice.oneEuroSettings.beta);

                    LogManager.LogInformation("{0} initialised as a {1}. Report interval set to {2}ms", this.ToString(), sensorFamily.ToString(), updateInterval);
                    break;
                case SensorFamily.SerialUSBIMU:
                    filter.SetFilterAttrs(((SerialUSBIMU)sensor).GetFilterCutoff(), ((SerialUSBIMU)sensor).GetFilterBeta());

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
                    ((Accelerometer)sensor).ReadingChanged += ReadingChanged;
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
                    ((Accelerometer)sensor).ReadingChanged -= ReadingChanged;
                    break;
                case SensorFamily.SerialUSBIMU:
                    ((SerialUSBIMU)sensor).ReadingChanged -= ReadingChanged;
                    break;
            }

            sensor = null;

            base.StopListening();
        }

        public void ReadingChanged(float GyroAccelX, float GyroAccelY, float GyroAccelZ)
        {
            switch (sensorFamily)
            {
                case SensorFamily.Controller:
                    {
                        this.reading.X = this.reading_fixed.X = GyroAccelX;
                        this.reading.Y = this.reading_fixed.Y = GyroAccelY;
                        this.reading.Z = this.reading_fixed.Z = GyroAccelZ;

                        base.ReadingChanged();
                    }
                    break;
            }
        }

        public void ReadingChanged(Vector3 AccelerationG, Vector3 AngularVelocityDeg)
        {
            this.reading.X = this.reading_fixed.X = (float)filter.axis1Filter.Filter(AccelerationG.X, IMU.DeltaSeconds);
            this.reading.Y = this.reading_fixed.Y = (float)filter.axis2Filter.Filter(AccelerationG.Y, IMU.DeltaSeconds);
            this.reading.Z = this.reading_fixed.Z = (float)filter.axis3Filter.Filter(AccelerationG.Z, IMU.DeltaSeconds);

            base.ReadingChanged();
        }

        private void ReadingChanged(Accelerometer sender, AccelerometerReadingChangedEventArgs args)
        {
            if (sensor is null)
                return;

            foreach (char axis in reading_axis.Keys)
            {
                switch (ControllerService.CurrentDevice.AccelerationAxisSwap[axis])
                {
                    default:
                    case 'X':
                        reading_axis[axis] = args.Reading.AccelerationX;
                        break;
                    case 'Y':
                        reading_axis[axis] = args.Reading.AccelerationY;
                        break;
                    case 'Z':
                        reading_axis[axis] = args.Reading.AccelerationZ;
                        break;
                }
            }

            this.reading.X = this.reading_fixed.X = (float)reading_axis['X'] * ControllerService.CurrentDevice.AccelerationAxis.X;
            this.reading.Y = this.reading_fixed.Y = (float)reading_axis['Y'] * ControllerService.CurrentDevice.AccelerationAxis.Y;
            this.reading.Z = this.reading_fixed.Z = (float)reading_axis['Z'] * ControllerService.CurrentDevice.AccelerationAxis.Z;

            base.ReadingChanged();
        }

        public Vector3 GetCurrentReading(bool center = false)
        {
            Vector3 reading = new Vector3()
            {
                X = center ? this.reading_fixed.X : this.reading.X,
                Y = center ? this.reading_fixed.Y : this.reading.Y,
                Z = center ? this.reading_fixed.Z : this.reading.Z
            };

            var readingZ = ControllerService.currentProfile.SteeringAxis == 0 ? reading.Z : reading.Y;
            var readingY = ControllerService.currentProfile.SteeringAxis == 0 ? reading.Y : -reading.Z;
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