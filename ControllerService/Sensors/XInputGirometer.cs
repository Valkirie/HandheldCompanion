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

        public XInputGirometer(XInputController controller, ILogger logger) : base(controller, logger)
        {
            updateTimer.Interval = updateInterval * 6;

            Gyrometer sensor = null; // Gyrometer.GetDefault();
            if (sensor != null)
            {
                sensor.ReportInterval = (uint)updateInterval;
                logger.LogInformation("{0} initialised. Report interval set to {1}ms", this.ToString(), sensor.ReportInterval);

                sensor.ReadingChanged += ReadingChanged;
            }
            else if (controller.USBGyro._serialPort.IsOpen)
            {
                controller.USBGyro.ReadingChanged += USBGyro_ReadingChanged;
                logger.LogInformation("{0} initialised. Baud rate to {1}", this.ToString(), controller.USBGyro._serialPort.BaudRate);
            }
            else
            {
                logger.LogWarning("{0} not initialised.", this.ToString());
            }
        }

        private void USBGyro_ReadingChanged(Vector3 AccelerationG, Vector3 AngularVelocityDeg)
        {
            this.reading.X = this.reading_fixed.X = (float)AngularVelocityDeg.X * controller.handheldDevice.AngularVelocityAxis.X;
            this.reading.Y = this.reading_fixed.Y = (float)AngularVelocityDeg.Y * controller.handheldDevice.AngularVelocityAxis.Y;
            this.reading.Z = this.reading_fixed.Z = (float)AngularVelocityDeg.Z * controller.handheldDevice.AngularVelocityAxis.Z;

            base.ReadingChanged();
        }

        private void ReadingChanged(Gyrometer sender, GyrometerReadingChangedEventArgs args)
        {
            GyrometerReading reading = args.Reading;

            // swapping Y and Z
            this.reading.X = this.reading_fixed.X = (float)reading.AngularVelocityX * controller.handheldDevice.AngularVelocityAxis.X;
            this.reading.Y = this.reading_fixed.Y = (float)reading.AngularVelocityZ * controller.handheldDevice.AngularVelocityAxis.Z;
            this.reading.Z = this.reading_fixed.Z = (float)reading.AngularVelocityY * controller.handheldDevice.AngularVelocityAxis.Y;

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

            if (controller.virtualTarget != null)
            {
                reading *= controller.profile.gyrometer;

                if (ratio)
                    reading.Y *= controller.handheldDevice.WidthHeightRatio;

                var readingZ = controller.profile.steering == 0 ? reading.Z : reading.Y;
                var readingY = controller.profile.steering == 0 ? reading.Y : reading.Z;
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

            return reading;
        }
    }
}
