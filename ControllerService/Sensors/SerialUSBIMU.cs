using System;
using System.IO.Ports;
using System.Numerics;
using Microsoft.Extensions.Logging;

// References
// https://www.demo2s.com/csharp/csharp-serialport-getportnames.html

namespace ControllerService.Sensors
{
	public class SerialUSBIMU
	{
		// Global variables that can be updated or output etc
		Vector3 AccelerationG = new Vector3();
		Vector3 AngularVelocityDeg = new Vector3();
		Vector3 EulerRollPitchYawDeg = new Vector3();

		// Create a new SerialPort object with default settings.
		private SerialPort SensorSerialPort = new SerialPort();

		// Todo, only once! Or based on reading if it's needed?
		private bool openAutoCalib = false;

		public SerialUSBIMU(ILogger logger)
		{
			string ComPortName = "";

			// Get a list of serial port names.
			string[] ports = SerialPort.GetPortNames();

			// Check if there are any serial connected devices
			if (ports.Length > 0)
			{
				// If only one device, use that.
				if (ports.Length == 1)
				{
					Console.WriteLine("USB Serial IMU using serialport: {0}", ports[0]);
					ComPortName = ports[0];
				}
				// In case of multiple devices, check them one by one
				if (ports.Length > 1)
				{
					Console.WriteLine("USB Serial IMU found multiple serialports, using: {0}", ports[0]);
					ComPortName = ports[0];
					// todo, check one by one if they report expected data, then choose that...
					// todo, if the device has a consistent (factory) name and manufacturer
				}
			}
			else
			{
				Console.WriteLine("USB Serial IMU no serialport device(s) detected.");
			}

			// If sensor is connected, configure and use.
			if (ComPortName != "") 
			{
				SensorSerialPort.PortName = ComPortName;
				SensorSerialPort.BaudRate = 115200;
				SensorSerialPort.DataBits = 8;
				SensorSerialPort.Parity = Parity.None;
				SensorSerialPort.StopBits = StopBits.One;
				SensorSerialPort.Handshake = Handshake.None;
				SensorSerialPort.RtsEnable = true;

				SensorSerialPort.Open();
				SensorSerialPort.DataReceived += new SerialDataReceivedEventHandler(DataReceivedHandler);

				// Todo, when to close?	

			}

		}

		// When data is received over the serial port		
		private void DataReceivedHandler(object sender, SerialDataReceivedEventArgs e)
		{
			int index = 0;
			byte[] byteTemp = new byte[1000];

			// Todo indata above to bytetemp

			// Read serial, store in byte array, at specified offset, certain amount and determine length
			UInt16 usLength = (UInt16)SensorSerialPort.Read(byteTemp, 0, 1000);

			// Default output mode is continues
			// Check frame header ID (default is 0xA4) and update rate 0x03 (default is 100 Hz 0x03)
			if ((byteTemp[index] == 0xA4) && (byteTemp[index + 1] == 0x03))
			{
				int datalength = 5 + byteTemp[index + 3];

				// If datalength is not 23 ie does not match the read register request
				// and does not match the start register number request 0x08
				// then request this...
				// Todo, assumption is that the continous output needs to be configured first to what you want (and saved!)?
				// If this assumption is correct, 
				if (datalength != 23 || byteTemp[index + 2] != 0x08)
				{
					// Initialization
					// Address write function code register = 0xA4, 0x03
					// Request to read data from register 08 (accel raw X and onward)
					// Number of registers wanted 0x12, 
					// Checksum 0xC1
					byte[] buffer = new byte[] { 0xA4, 0x03, 0x08, 0x12, 0xC1 };
					SensorSerialPort.Write(buffer, 0, buffer.Length);
					index += usLength; // Remaining data is not processed
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
					SensorSerialPort.Write(buffer, 0, buffer.Length);
					System.Threading.Thread.Sleep(1);
					// Address write function code register = 0xA4, 0x03
					// Register to read/write save settings 0x05
					// 0x55 save current configuration
					buffer = new byte[] { 0xA4, 0x06, 0x05, 0x55, 0x04 };
					SensorSerialPort.Write(buffer, 0, buffer.Length);
					openAutoCalib = false;
				}

				byte[] array = new byte[datalength];
				Array.ConstrainedCopy(byteTemp, index, array, 0, datalength);
				InterpretData(array);
				index += datalength;
			}
		}

		// Convert raw bytes to SI unit variables
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
			IntData[6] = (short)((byteTemp[16] << 8) | byteTemp[17]);
			IntData[7] = (short)((byteTemp[18] << 8) | byteTemp[19]);
			IntData[8] = (short)((byteTemp[20] << 8) | byteTemp[21]);

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

			// Todo, according to spec sheet 6 - 8 contain roll pitch yaw... check usability.
			// Roll, Pitch, Yaw
		    EulerRollPitchYawDeg.X = (float)(IntData[6] / 32768.0);
			EulerRollPitchYawDeg.Y = (float)(IntData[7] / 32768.0);
			EulerRollPitchYawDeg.Z = (float)(IntData[8] / 32768.0);
		}

		// Todo, profile swapping etc?

		// Todo use or not use get currents?
		public Vector3 GetCurrentReadingAcc()
		{
			return AccelerationG;
		}

		public Vector3 GetCurrentReadingAngVel()
		{
			return AngularVelocityDeg;
		}

		public Vector3 GetCurrentReadingRollPitchYaw()
		{
			return EulerRollPitchYawDeg;
		}
	}
}
