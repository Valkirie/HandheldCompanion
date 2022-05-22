using System;
using System.Numerics;
using Microsoft.Extensions.Logging;
using System.Linq;
using static ControllerCommon.Utils.DeviceUtils;
using static ControllerCommon.Utils.CommonUtils;
using ControllerCommon;
using System.Threading;
using System.IO.Ports;

namespace ControllerCommon
{
	public class SerialUSBIMU
	{
		// Global variables that can be updated or output etc
		private Vector3 AccelerationG = new Vector3();		// accelerometer
		private Vector3 AngularVelocityDeg = new Vector3(); // gyrometer

		private OneEuroFilter3D accelerationFilter;

		public USBDeviceInfo sensor;
		private SerialPort serial;

		private bool openAutoCalib = false; // Todo, only once! Or based on reading if it's needed?

		private ILogger logger;

		public event ReadingChangedEventHandler ReadingChanged;
		public delegate void ReadingChangedEventHandler(Vector3 AccelerationG, Vector3 AngularVelocityDeg);

		public event ConnectedEventHandler Connected;
		public delegate void ConnectedEventHandler();

		public event DisconnectedEventHandler Disconnected;
		public delegate void DisconnectedEventHandler();

		double FilterRate = 100;

		public static SerialUSBIMU GetDefault(ILogger logger = null)
		{
			SerialUSBIMU serialUSBIMU = new SerialUSBIMU();
			serialUSBIMU.logger = logger;	

			serialUSBIMU.accelerationFilter = new OneEuroFilter3D();
			serialUSBIMU.accelerationFilter.SetFilterAttrs(0.008, 0.001);

			// USB Gyro v2 COM Port settings.
			serialUSBIMU.serial = new SerialPort()
			{
				BaudRate = 115200, // Differs from datasheet intentionally.
				DataBits = 8,
				Parity = Parity.None,
				StopBits = StopBits.One,
				Handshake = Handshake.None,
				RtsEnable = true,
				ReadTimeout = 500,
				WriteTimeout = 500
			};

			serialUSBIMU.DeviceEvent(false);

			return serialUSBIMU;
		}

		public override string ToString()
		{
			return this.GetType().Name;
		}

		public bool IsOpen()
        {
			return serial.IsOpen;
        }

		public int GetInterval()
        {
			return serial.BaudRate;
        }

		// Check for all existing connected devices,
		// if match is found for Gyro USB v2,
		// connect and log info accordingly.
		public void DeviceEvent(bool update)
		{
			try
			{
				// get the first available USB gyro sensor
				var serial = GetSerialDevices().Where(a => a.PID.Equals("7523") && a.VID.Equals("1A86")).FirstOrDefault();
				if (serial != null)
				{
					if (this.serial.IsOpen)
						return;

					logger?.LogInformation("{0} connecting to {1}", this.ToString(), serial.Name);

					// give system a bit of time...
					Thread.Sleep(1000);

					// update current sensor
					sensor = serial;

					string[] SplitName = sensor.Name.Split(' ');
					this.serial.PortName = SplitName[2].Trim('(').Trim(')');
					this.serial.Open();
					this.serial.DataReceived += new SerialDataReceivedEventHandler(DataReceivedHandler);

					logger?.LogInformation("{0} connected to {1}", this.ToString(), sensor.Name);
					Connected?.Invoke();
				}
				else if (sensor != null)
				{
					this.serial.Close();
					this.serial.DataReceived -= new SerialDataReceivedEventHandler(DataReceivedHandler);

					logger?.LogInformation("{0} disconnected from {1}", this.ToString(), sensor.Name);
					Disconnected?.Invoke();
				}
			}
			catch (Exception ex)
			{
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
				usLength = (ushort)serial.Read(byteTemp, 0, 1000);
			}catch (Exception)
			{
				return;
			}

			// Default output mode is continues
			// Check frame header ID (default is 0xA4) and update rate 0x03 (default is 100 Hz 0x03)
			if ((byteTemp[index] == 0xA4) && (byteTemp[index + 1] == 0x03))
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
						serial.Write(buffer, 0, buffer.Length);
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
						serial.Write(buffer, 0, buffer.Length);
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

					serial.Write(buffer, 0, buffer.Length);
					openAutoCalib = false;
				}

				byte[] array = new byte[datalength];
				Array.ConstrainedCopy(byteTemp, index, array, 0, datalength);

				InterpretData(array);
				FilterData();
				PlacementTransformation("Top", false);

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
			IntData[0] = (short)((byteTemp[4] << 8) | byteTemp[5]);
			IntData[1] = (short)((byteTemp[6] << 8) | byteTemp[7]);
			IntData[2] = (short)((byteTemp[8] << 8) | byteTemp[9]);
			IntData[3] = (short)((byteTemp[10] << 8) | byteTemp[11]);
			IntData[4] = (short)((byteTemp[12] << 8) | byteTemp[13]);
			IntData[5] = (short)((byteTemp[14] << 8) | byteTemp[15]);

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

		public void FilterData()
        {
			AccelerationG.X = (float)accelerationFilter.axis1Filter.Filter(AccelerationG.X, FilterRate);
			AccelerationG.Y = (float)accelerationFilter.axis2Filter.Filter(AccelerationG.Y, FilterRate);
			AccelerationG.Z = (float)accelerationFilter.axis3Filter.Filter(AccelerationG.Z, FilterRate);
		}

		public void UpdateFilterRate(double DeltaSecond)
		{
			FilterRate = 1 / DeltaSecond;
		}

		public void PlacementTransformation(string PlacementPosition, bool Mirror)
		{
			// Adaption of XYZ or invert based on USB port location on device. 
			// Mirror option in case of USB-C port usage. Pins on screen side is default.

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

			switch (PlacementPosition)
			{
				case "Top":
					AccelerationG.X = -AccTemp.X;

					if (Mirror) {
						AccelerationG.X = -AccTemp.X; // Yes, this is applied twice intentionally!
						AccelerationG.Y = -AccTemp.Y;

						AngularVelocityDeg.X = -AngVelTemp.X;
						AngularVelocityDeg.Y = -AngVelTemp.Y;
					}

					break;
				case "Right":

					if (Mirror) { }

					break;
				case "Bottom":

					AccelerationG.Z = -AccTemp.Z;

					AngularVelocityDeg.X = -AngVelTemp.X;
					AngularVelocityDeg.Z = -AngVelTemp.Z;

					if (Mirror) { }

					break;
				case "Left":

					if (Mirror) { }
					break;
				default:
					break;
			}
		}	
	}
}
