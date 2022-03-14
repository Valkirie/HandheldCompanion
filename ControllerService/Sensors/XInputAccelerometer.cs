using ControllerCommon;
using Microsoft.Extensions.Logging;
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
                sensor.ReportInterval = (uint)updateInterval;
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

            this.reading.X = (float)reading.AccelerationX;
            this.reading.Y = (float)reading.AccelerationZ;
            this.reading.Z = (float)reading.AccelerationY;

            Task.Run(() => logger?.LogDebug("XInputAccelerometer.ReadingChanged({0:00.####}, {1:00.####}, {2:00.####})", this.reading.X, this.reading.Y, this.reading.Z));
        }

        private void Shaken(Accelerometer sender, AccelerometerShakenEventArgs args)
        {
            // throw new NotImplementedException();
        }

        public new Vector3 GetCurrentReading(bool center = false)
        {
            Vector3 reading = new Vector3()
            {
                X = center ? this.reading_fixed.X : this.reading.X,
                Y = center ? this.reading_fixed.Y : this.reading.Y,
                Z = center ? this.reading_fixed.Z : this.reading.Z
            };

            if (controller.virtualTarget != null)
            {
                reading *= controller.profile.accelerometer;

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

            return reading;
        }
    }
}