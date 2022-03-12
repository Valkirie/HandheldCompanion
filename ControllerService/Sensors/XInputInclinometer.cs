using ControllerCommon;
using Microsoft.Extensions.Logging;
using System;
using System.Numerics;
using System.Threading.Tasks;
using Windows.Devices.Sensors;
using SensorType = ControllerCommon.SensorType;

namespace ControllerService.Sensors
{
    public class XInputInclinometer : XInputSensor
    {
        public Accelerometer sensor;

        public event ReadingChangedEventHandler ReadingHasChanged;
        public delegate void ReadingChangedEventHandler(XInputInclinometer sender, Vector3 e);

        private readonly ILogger logger;

        public XInputInclinometer(XInputController controller, ILogger logger, PipeServer pipeServer) : base(controller, pipeServer)
        {
            this.logger = logger;

            sensor = Accelerometer.GetDefault();
            if (sensor != null)
            {
                sensor.ReportInterval = (uint)controller.updateInterval;
                logger.LogInformation("{0} initialised. Report interval set to {1}ms", this.ToString(), sensor.ReportInterval);
                sensor.ReadingChanged += ReadingChanged;
            }
            else
            {
                logger.LogInformation("{0} not initialised.", this.ToString());
            }
        }

        private void ReadingChanged(Accelerometer sender, AccelerometerReadingChangedEventArgs args)
        {
            AccelerometerReading reading = args.Reading;

            this.reading.X = (float)reading.AccelerationX;
            this.reading.Y = (float)reading.AccelerationZ;
            this.reading.Z = (float)reading.AccelerationY;

            logger?.LogDebug("XInputInclinometer.ReadingChanged({0:00.####}, {1:00.####}, {2:00.####})", this.reading.X, this.reading.Y, this.reading.Z);

            // update client(s)
            if (ControllerService.CurrentTag == "ProfileSettingsMode1")
                pipeServer?.SendMessage(new PipeSensor(this.reading, SensorType.Inclinometer));
        }

        public override string ToString()
        {
            return this.GetType().Name;
        }

        public Vector3 GetCurrentReading()
        {
            // Y up coordinate system, swap Y and Z.
            // Duplicate values to allow for optional swapping or inverting.
            float readingX = this.reading.X;
            float readingY = this.reading.Y;
            float readingZ = this.reading.Z;

            if (controller.virtualTarget != null)
            {
                // Allow for user swapping X and Y axis.
                this.reading.Z = controller.profile.steering == 0 ? readingZ : readingY;
                this.reading.Y = controller.profile.steering == 0 ? readingY : -readingZ;
                this.reading.X = controller.profile.steering == 0 ? readingX : readingX;

                // Allow for user inverting X or Y axis direction.
                if (controller.profile.inverthorizontal)
                {
                    this.reading.Y *= -1.0f;
                    this.reading.Z *= -1.0f;
                }

                if (controller.profile.invertvertical)
                {
                    this.reading.Y *= -1.0f;
                    this.reading.X *= -1.0f;
                }
            }

            // Calculate angles around Y and X axis (Theta and Psi) using all 3 directions of accelerometer
            // Based on: https://www.digikey.com/en/articles/using-an-accelerometer-for-inclination-sensing               
            double angle_x_psi = -1 * (Math.Atan(this.reading.Y / (Math.Sqrt(Math.Pow(this.reading.X, 2) + Math.Pow(this.reading.Z, 2))))) * 180 / Math.PI;
            double angle_y_theta = -1 * (Math.Atan(this.reading.X / (Math.Sqrt(Math.Pow(this.reading.Y, 2) + Math.Pow(this.reading.Z, 2))))) * 180 / Math.PI;

            this.reading.X = (float)(angle_x_psi);
            this.reading.Y = (float)(angle_y_theta);

            return this.reading;
        }
    }
}