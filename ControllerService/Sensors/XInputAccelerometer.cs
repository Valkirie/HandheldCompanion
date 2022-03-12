using ControllerCommon;
using Microsoft.Extensions.Logging;
using System;
using System.Numerics;
using System.Threading.Tasks;
using Windows.Devices.Sensors;
using static ControllerCommon.Utils;

namespace ControllerService.Sensors
{
    public class XInputAccelerometer : XInputSensor
    {
        public Accelerometer sensor;
        public static SensorSpec sensorSpec = new SensorSpec()
        {
            minIn = -2.0f,
            maxIn = 2.0f,
            minOut = short.MinValue,
            maxOut = short.MaxValue,
        };

        public event ReadingChangedEventHandler ReadingHasChanged;
        public delegate void ReadingChangedEventHandler(XInputAccelerometer sender, Vector3 e);

        private readonly ILogger logger;

        public XInputAccelerometer(XInputController controller, ILogger logger, PipeServer pipeServer) : base(controller, pipeServer)
        {
            this.logger = logger;

            sensor = Accelerometer.GetDefault();
            if (sensor != null)
            {
                sensor.ReportInterval = (uint)controller.updateInterval;
                logger.LogInformation("{0} initialised. Report interval set to {1}ms", this.ToString(), sensor.ReportInterval);

                sensor.ReadingChanged += ReadingChanged;
                sensor.Shaken += Shaken;
            }
            else
            {
                logger.LogWarning("{0} not initialised.", this.ToString());
            }
        }

        private void ReadingChanged(Accelerometer sender, AccelerometerReadingChangedEventArgs args)
        {
            AccelerometerReading reading = args.Reading;

            float readingX = this.reading.X = (float)reading.AccelerationX;
            float readingY = this.reading.Y = (float)reading.AccelerationZ;
            float readingZ = this.reading.Z = (float)reading.AccelerationY;

            // apply profile
            if (controller.virtualTarget != null)
            {
                this.reading *= controller.profile.accelerometer;

                this.reading.Z = controller.profile.steering == 0 ? readingZ : readingY;
                this.reading.Y = controller.profile.steering == 0 ? readingY : -readingZ;
                this.reading.X = controller.profile.steering == 0 ? readingX : readingX;

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

            logger?.LogDebug("XInputAccelerometer.ReadingChanged({0:00.####}, {1:00.####}, {2:00.####})", this.reading.X, this.reading.Y, this.reading.Z);
        }

        private void Shaken(Accelerometer sender, AccelerometerShakenEventArgs args)
        {
            return; // implement me
            throw new NotImplementedException();
        }

        public override string ToString()
        {
            return this.GetType().Name;
        }

        public Vector3 GetCurrentReading()
        {
            return this.reading;
        }
    }
}