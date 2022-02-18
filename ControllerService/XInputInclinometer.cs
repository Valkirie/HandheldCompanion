using Microsoft.Extensions.Logging;
using System;
using System.Diagnostics;
using System.Numerics;
using Windows.Devices.Sensors;
using static ControllerCommon.Utils;

namespace ControllerService
{
    public class XInputInclinometer
    {
        public Accelerometer sensor;
        private Vector3 reading = new();

        public event ReadingChangedEventHandler ReadingHasChanged;
        public delegate void ReadingChangedEventHandler(XInputInclinometer sender, Vector3 e);

        private readonly ILogger logger;
        private readonly XInputController xinput;

        public XInputInclinometer(XInputController controller, ILogger logger)
        {
            this.logger = logger;
            this.xinput = controller;

            sensor = Accelerometer.GetDefault();

            if (sensor != null)
            {
                sensor.ReportInterval = sensor.MinimumReportInterval;
                logger.LogInformation("{0} initialised. Report interval set to {1}ms", this.ToString(), sensor.ReportInterval);

                sensor.ReadingChanged += ReadingChanged;
            }
            else
            {
                logger.LogInformation("{0} not initialised.", this.ToString());
            }
        }

        public override string ToString()
        {
            return this.GetType().Name;
        }

        private void ReadingChanged(Accelerometer sender, AccelerometerReadingChangedEventArgs args)
        {
            AccelerometerReading reading = args.Reading;

            // Y up coordinate system, swap Y and Z.
            // Duplicate values to allow for optional swapping or inverting.
            float readingX = this.reading.X = (float)reading.AccelerationX;
            float readingY = this.reading.Y = (float)reading.AccelerationZ;
            float readingZ = this.reading.Z = (float)reading.AccelerationY;

            if (xinput.virtualTarget != null)
            {
                // Allow for user swapping X and Y axis.
                this.reading.Z = xinput.profile.steering == 0 ? readingZ : readingY;
                this.reading.Y = xinput.profile.steering == 0 ? readingY : -readingZ;
                this.reading.X = xinput.profile.steering == 0 ? readingX : readingX;

                // Allow for user inverting X or Y axis direction.
                if (xinput.profile.inverthorizontal)
                {
                    this.reading.Y *= -1.0f;
                    this.reading.Z *= -1.0f;
                }

                if (xinput.profile.invertvertical)
                {
                    this.reading.Y *= -1.0f;
                    this.reading.X *= -1.0f;
                }
            }

            // Calculate angles around Y and X axis (Theta and Psi) using all 3 directions of accelerometer
            // Based on: https://www.digikey.com/en/articles/using-an-accelerometer-for-inclination-sensing               
            double angle_x_psi = -1 * (Math.Atan(this.reading.Y / (Math.Sqrt(Math.Pow(this.reading.X, 2) + Math.Pow(this.reading.Z, 2))))) * 180 / Math.PI;
            double angle_y_theta = -1 * (Math.Atan(this.reading.X / (Math.Sqrt(Math.Pow(this.reading.Y, 2) + Math.Pow(this.reading.Z, 2))))) * 180 / Math.PI;

            logger?.LogInformation("Axis angles X: {0:00.####}, Y: {1:00.####}", angle_x_psi, angle_y_theta);

            this.reading.X = (float)(angle_x_psi);
            this.reading.Y = (float)(angle_y_theta);            

            // Raise event
            ReadingHasChanged?.Invoke(this, this.reading);
        }
    }
}
