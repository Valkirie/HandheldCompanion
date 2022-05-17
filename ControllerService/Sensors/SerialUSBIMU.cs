using System;
using System.IO.Ports;
using System.Numerics;
using Microsoft.Extensions.Logging;
using System.Management;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Linq;

// References
// https://www.demo2s.com/csharp/csharp-serialport-getportnames.html
// https://www.sparxeng.com/blog/software/must-use-net-system-io-ports-serialport
// http://blog.gorski.pm/serial-port-details-in-c-sharp
// https://github.com/freakone/serial-reader/blob/744e4337cb380cb9ce1ad6067f9eecf7917019c6/SerialReader/MainWindow.xaml.cs#L79

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

		private bool openAutoCalib = false; // Todo, only once! Or based on reading if it's needed?
		private readonly ILogger logger;

		// COM Port struct with wanted properties
		struct ComPort 
		{
			public string name;
			public string vid;
			public string pid;
			public string description;
		}

		public SerialUSBIMU(ILogger logger)
		{
			this.logger = logger;

			// USB Gyro v2 COM Port settings.
			SensorSerialPort.BaudRate = 115200; // Differs from datasheet intentionally.
			SensorSerialPort.DataBits = 8;
			SensorSerialPort.Parity = Parity.None;
			SensorSerialPort.StopBits = StopBits.One;
			SensorSerialPort.Handshake = Handshake.None;
			SensorSerialPort.RtsEnable = true;

			// Todo, rework to timer that runs/checks every second
			DetectAndConnect();
		}

		// Check for all existing connected devices,
		// if match is found for Gyro USB v2,
		// connect and log info accordingly.
		public void DetectAndConnect()
		{
			// Get a list of serial ports and their properties.
			List<ComPort> PortList = GetSerialPorts();
			logger.LogInformation("USB Serial IMU detected {0} COM devices", PortList.Count);

			// Handle possible exceptions.
			try
			{
				// Check if there are any serial connected devices
				if (PortList.Count > 0)
				{
					// Filter ports to specific PID and VID of USB Gyro v2
					ComPort com = PortList.FindLast(c => c.pid.Equals("7523") && c.vid.Equals("1A86"));

					// Interprete name, example: USB-SERIAL CH340 (COM4)
					string[] SplitName = com.name.Split(' ');
					SensorSerialPort.PortName = SplitName[2].Trim('(').Trim(')');

					if (SensorSerialPort.IsOpen)
						SensorSerialPort.Close();

					SensorSerialPort.Open();
					SensorSerialPort.DataReceived += DataReceivedHandler;

					logger.LogInformation("USB Serial IMU Connected to {0}", com.name);
				}
				else
				{
					logger.LogInformation("USB Serial IMU no serialport device(s) detected.");
				}
			}
			catch (Exception ex)
			{
				logger.LogInformation("USB Serial IMU exception occured {0}", ex.ToString());
			}

		}

		// Get all current serial ports with property information
		private List<ComPort> GetSerialPorts()
		{
			using (var searcher = new ManagementObjectSearcher
				("SELECT * FROM Win32_PnPEntity WHERE Name LIKE '%COM%' AND PNPClass = 'Ports'"))
			{
				var ports = searcher.Get().Cast<ManagementBaseObject>().ToList();
				return ports.Select(p =>
				{
					ComPort c = new ComPort();
					c.name = p.GetPropertyValue("Name").ToString();
					c.vid = p.GetPropertyValue("PNPDeviceID").ToString();
					c.description = p.GetPropertyValue("Caption").ToString();

					string vidPattern = @"VID_([0-9A-F]{4})";
					string pidPattern = @"PID_([0-9A-F]{4})";

					Match mVID = Regex.Match(c.vid, vidPattern, RegexOptions.IgnoreCase);
					Match mPID = Regex.Match(c.vid, pidPattern, RegexOptions.IgnoreCase);

					if (mVID.Success)
						c.vid = mVID.Groups[1].Value;
					if (mPID.Success)
						c.pid = mPID.Groups[1].Value;

					return c;

				}).ToList();
			}
		}

		// When data is received over the serial port, parse.	
		private void DataReceivedHandler(object sender, SerialDataReceivedEventArgs e)
		{
			int index = 0;
			byte[] byteTemp = new byte[1000];

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
				PlacementTransformation("Top", false);

				index += datalength; // Todo, check with Frank, = 0 probably in the wrong location.
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

		public void PlacementTransformation(string PlacementPosition, bool Mirror)
		{
			// Adaption of XYZ or invert based on USB port location on device. 
			// Mirror option in case of USB-C port usage.

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

		// Todo, call when application closes and USB device is no longer detected.
		public void Disconnect()
		{
			if (SensorSerialPort.IsOpen)
			{
				SensorSerialPort.Close();
			}
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
	}
}
