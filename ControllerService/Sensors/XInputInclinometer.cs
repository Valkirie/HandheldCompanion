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
                sensor.ReportInterval = (uint)updateInterval;
                logger.LogInformation("{0} initialised. Report interval set to {1}ms", this.ToString(), sensor.ReportInterval);
                sensor.ReadingChanged += ReadingChanged;
            }
            else
            {
                logger.LogWarning("{0} not initialised.", this.ToString());
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
            Vector3 reading = new Vector3(this.reading.X, this.reading.Y, this.reading.Z);

            if (controller.virtualTarget != null)
            {
                reading.Z = controller.profile.steering == 0 ? this.reading.Z : this.reading.Y;
                reading.Y = controller.profile.steering == 0 ? this.reading.Y : -this.reading.Z;
                reading.X = controller.profile.steering == 0 ? this.reading.X : this.reading.X;

                if (controller.profile.inverthorizontal)
                {
                    reading.Y *= -1.0f;
                    reading.Z *= -1.0f;
                }

                if (controller.profile.invertvertical)
                {
                    reading.Y *= -1.0f;
                    reading.X *= -1.0f;
                }
            }

            // Calculate angles around Y and X axis (Theta and Psi) using all 3 directions of accelerometer
            // Based on: https://www.digikey.com/en/articles/using-an-accelerometer-for-inclination-sensing               
            double angle_x_psi = -1 * (Math.Atan(reading.Y / (Math.Sqrt(Math.Pow(reading.X, 2) + Math.Pow(reading.Z, 2))))) * 180 / Math.PI;
            double angle_y_theta = -1 * (Math.Atan(reading.X / (Math.Sqrt(Math.Pow(reading.Y, 2) + Math.Pow(reading.Z, 2))))) * 180 / Math.PI;

            reading.X = (float)(angle_x_psi);
            reading.Y = (float)(angle_y_theta);

            return reading;
        }

        public Vector3 GetCurrentReadingRaw()
        {
            return this.reading;
        }
    }
}