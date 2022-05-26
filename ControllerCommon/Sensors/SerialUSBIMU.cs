using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
using System.Numerics;
using System.Threading;
using static ControllerCommon.OneEuroFilter;
using static ControllerCommon.Utils.CommonUtils;
using static ControllerCommon.Utils.DeviceUtils;

namespace ControllerCommon.Sensors
{
    public enum SerialPlacement
    {
        Top = 0,
        Left = 1,
        Right = 2,
        Bottom = 3
    }

    public class SerialUSBIMU
    {
        // Global variables that can be updated or output etc
        private Vector3 AccelerationG = new Vector3();      // accelerometer
        private Vector3 AngularVelocityDeg = new Vector3(); // gyrometer

        private USBDeviceInfo device;
        private SerialPortEx port = new();
        private static SerialUSBIMU serial = new();

        public static Dictionary<KeyValuePair<string, string>, SerialPortEx> vendors = new()
        {
			// USB Gyro v2
			{
                new KeyValuePair<string, string>("1A86", "7523"),
                new SerialPortEx() {
                    BaudRate = 115200, DataBits = 8, Parity = Parity.None, StopBits = StopBits.One,
                    Handshake = Handshake.None, RtsEnable = true, ReadTimeout = 500, WriteTimeout = 500,
                    oneEuroSettings = new OneEuroSettings(0.001d, 0.008d)
                }
            }
        };

        private SerialPlacement SensorPlacement = SerialPlacement.Top;
        private bool isUpsideDown = false;
        private bool openAutoCalib = false;     // todo: only once! Or based on reading if it's needed?

        private ILogger logger;

        public event ReadingChangedEventHandler ReadingChanged;
        public delegate void ReadingChangedEventHandler(Vector3 AccelerationG, Vector3 AngularVelocityDeg);

        public static SerialUSBIMU GetDefault(ILogger logger = null)
        {
            if (serial.port.IsOpen)
                return serial;

            serial.logger = logger;

            USBDeviceInfo deviceInfo = null;
            List<USBDeviceInfo> devices = GetSerialDevices();

            foreach (var sensor in vendors)
            {
                string VendorID = sensor.Key.Key;
                string ProductID = sensor.Key.Value;

                deviceInfo = devices.Where(a => a.VID == VendorID && a.PID == ProductID).FirstOrDefault();
                if (deviceInfo != null)
                {
                    serial.device = deviceInfo;
                    serial.port = sensor.Value;
                    serial.port.PortName = Between(deviceInfo.Name, "(", ")");
                    break;
                }
            }

            if (deviceInfo is null)
                return null;

            return serial;
        }

        public override string ToString()
        {
            return GetType().Name;
        }

        public bool IsOpen()
        {
            return port.IsOpen;
        }

        public int GetInterval()
        {
            return port.BaudRate;
        }

        public string GetName()
        {
            return device != null ? device.Name : "N/A";
        }

        public double GetFilterCutoff()
        {
            return port.oneEuroSettings.minCutoff;
        }

        public double GetFilterBeta()
        {
            return port.oneEuroSettings.beta;
        }

        private int tentative;
        private int maxTentative = 8;
        public bool Open()
        {
            tentative = 0; // reset tentative

            logger?.LogInformation("{0} connecting to {1}", serial.ToString(), serial.device.Name);

            while (!serial.port.IsOpen && tentative < maxTentative)
            {
                try
                {
                    serial.port.Open();
                    serial.port.DataReceived += new SerialDataReceivedEventHandler(serial.DataReceivedHandler);

                    logger?.LogInformation("{0} connected", serial.ToString());
                    return true;
                }
                catch (Exception)
                {
                    // port is not ready yet
                    tentative++;
                    logger?.LogError("{0} could not connect. Attempt: {1} out of {2}", serial.ToString(), tentative, maxTentative);
                    Thread.Sleep(500);
                }
            }

            return false;
        }

        public bool Close()
        {
            try
            {
                serial.port.Close();
                serial.port.DataReceived -= new SerialDataReceivedEventHandler(serial.DataReceivedHandler);

                logger?.LogInformation("{0} disconnected", serial.ToString());
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        // When data is received over the serial port, parse.	
        private void DataReceivedHandler(object sender, SerialDataReceivedEventArgs e)
        {
            int index = 0;
            ushort usLength;
            byte[] byteTemp = new byte[1000];

            try
            {
                // Read serial, store in byte array, at specified offset, certain amount and determine length
                usLength = (ushort)port.Read(byteTemp, 0, 1000);
            }
            catch (Exception)
            {
                return;
            }

            // Default output mode is continues
            // Check frame header ID (default is 0xA4) and update rate 0x03 (default is 100 Hz 0x03)
            if (byteTemp[index] == 0xA4 && byteTemp[index + 1] == 0x03)
            {
                int datalength = 5 + byteTemp[index + 3];

                // If datalength is not 23 ie does not match the read register request
                // and does not match the start register number request 0x08
                // then request this...
                if (datalength != 23 || byteTemp[index + 2] != 0x08)
                {
                    // Initialization
                    // Address write function code register = 0xA4, 0x03
                    // Request to read data from register 08 (accel raw X and onward)
                    // Number of registers wanted 0x12, 
                    // Checksum 0xC1
                    byte[] buffer = new byte[] { 0xA4, 0x03, 0x08, 0x12, 0xC1 };

                    logger.LogInformation("Serial USB Received unexpected datalength and start register, setting register...");

                    try
                    {
                        port.Write(buffer, 0, buffer.Length);
                    }
                    catch (Exception)
                    {
                        return;
                    }

                    return;
                }

                // Determination calibration
                if (openAutoCalib == true)
                {
                    // Initialization
                    // Address write function code register = 0xA4, 0x03
                    // Register to read/write 0x07 Status query
                    // Data checksum lower 8 bits  0x10
                    byte[] buffer = new byte[] { 0xA4, 0x06, 0x07, 0x5F, 0x10 };

                    logger?.LogInformation("Serial USB Calibrating Sensor");

                    try
                    {
                        port.Write(buffer, 0, buffer.Length);
                    }
                    catch (Exception)
                    {
                        return;
                    }

                    Thread.Sleep(1); // give device a bit of time...

                    // Address write function code register = 0xA4, 0x03
                    // Register to read/write save settings 0x05
                    // 0x55 save current configuration
                    buffer = new byte[] { 0xA4, 0x06, 0x05, 0x55, 0x04 };

                    logger?.LogInformation("Serial USB save settings on device");

                    port.Write(buffer, 0, buffer.Length);
                    openAutoCalib = false;
                }

                byte[] array = new byte[datalength];
                Array.ConstrainedCopy(byteTemp, index, array, 0, datalength);

                InterpretData(array);
                PlacementTransformation(SensorPlacement, isUpsideDown);

                // raise event
                ReadingChanged?.Invoke(AccelerationG, AngularVelocityDeg);
            }
        }

        // Convert raw bytes to SI units
        public void InterpretData(byte[] byteTemp)
        {
            // Array to interprete bytes
            short[] IntData = new short[9];

            // Byte bit ranges to int conversion
            IntData[0] = (short)(byteTemp[4] << 8 | byteTemp[5]);
            IntData[1] = (short)(byteTemp[6] << 8 | byteTemp[7]);
            IntData[2] = (short)(byteTemp[8] << 8 | byteTemp[9]);
            IntData[3] = (short)(byteTemp[10] << 8 | byteTemp[11]);
            IntData[4] = (short)(byteTemp[12] << 8 | byteTemp[13]);
            IntData[5] = (short)(byteTemp[14] << 8 | byteTemp[15]);

            // Acceleration, convert byte to G
            // Assuming default range
            // Flip Y and Z
            AccelerationG.X = (float)(IntData[0] / 32768.0 * 16);
            AccelerationG.Z = (float)(IntData[1] / 32768.0 * 16);
            AccelerationG.Y = (float)(IntData[2] / 32768.0 * 16);

            // Gyro, convert byte to angular velocity deg/sec
            // Assuming default range
            // Flip Y and Z
            AngularVelocityDeg.X = (float)(IntData[3] / 32768.0 * 2000);
            AngularVelocityDeg.Z = (float)(IntData[4] / 32768.0 * 2000);
            AngularVelocityDeg.Y = (float)(IntData[5] / 32768.0 * 2000);
        }

        public void PlacementTransformation(SerialPlacement SensorPlacement, bool isUpsideDown)
        {
            // Adaption of XYZ or invert based on USB port location on device. 
            // Upsidedown option in case of USB-C port usage. Pins on screen side is default.

            Vector3 AccTemp = AccelerationG;
            Vector3 AngVelTemp = AngularVelocityDeg;

            /*
					Convenient default copy paste list.
					
					AccelerationG.X = AccTemp.X;
					AccelerationG.Y = AccTemp.Y;
					AccelerationG.Z = AccTemp.Z;

					AngularVelocityDeg.X = AngVelTemp.X;
					AngularVelocityDeg.Y = AngVelTemp.Y;
					AngularVelocityDeg.Z = AngVelTemp.Z; 
			*/

            switch (SensorPlacement)
            {
                case SerialPlacement.Top:
                    {
                        AccelerationG.X = -AccTemp.X;

                        if (isUpsideDown)
                        {
                            AccelerationG.X = -AccTemp.X; // Yes, this is applied twice intentionally!
                            AccelerationG.Y = -AccTemp.Y;

                            AngularVelocityDeg.X = -AngVelTemp.X;
                            AngularVelocityDeg.Y = -AngVelTemp.Y;
                        }
                    }
                    break;
                case SerialPlacement.Right:
                    {
                        if (isUpsideDown)
                        {
                            // do something
                        }
                    }
                    break;
                case SerialPlacement.Bottom:
                    {
                        AccelerationG.Z = -AccTemp.Z;

                        AngularVelocityDeg.X = -AngVelTemp.X;
                        AngularVelocityDeg.Z = -AngVelTemp.Z;

                        if (isUpsideDown)
                        {
                            // do something
                        }
                    }
                    break;
                case SerialPlacement.Left:
                    {
                        if (isUpsideDown)
                        {
                            // do something
                        }
                    }
                    break;
                default:
                    break;
            }
        }

        public void SetSensorPlacement(SerialPlacement SensorPlacement, bool isUpsideDown)
        {
            this.SensorPlacement = SensorPlacement;
            this.isUpsideDown = isUpsideDown;
        }
    }
}
