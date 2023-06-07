using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using ControllerCommon.Managers;
using static ControllerCommon.OneEuroFilter;
using static ControllerCommon.Utils.CommonUtils;
using static ControllerCommon.Utils.DeviceUtils;

namespace ControllerCommon.Sensors;

public enum SerialPlacement
{
    Top = 0,
    Left = 1,
    Right = 2,
    Bottom = 3
}

public class SerialUSBIMU
{
    public delegate void ReadingChangedEventHandler(Vector3 AccelerationG, Vector3 AngularVelocityDeg);

    private static readonly SerialUSBIMU serial = new();

    public static Dictionary<KeyValuePair<string, string>, SerialPortEx> vendors = new()
    {
        // USB Gyro v2
        {
            new KeyValuePair<string, string>("1A86", "7523"),
            new SerialPortEx
            {
                BaudRate = 115200, DataBits = 8, Parity = Parity.None, StopBits = StopBits.One,
                Handshake = Handshake.None, RtsEnable = true, ReadTimeout = 500, WriteTimeout = 500,
                oneEuroSettings = new OneEuroSettings(0.001d, 0.008d)
            }
        }
    };

    private Vector3 AccelerationG; // accelerometer
    private Vector3 AngularVelocityDeg; // gyrometer

    private USBDeviceInfo device;
    private bool isUpsideDown;
    private readonly int maxTentative = 8;
    private bool openAutoCalib; // todo: only once! Or based on reading if it's needed?
    private SerialPortEx port = new();

    private SerialPlacement SensorPlacement = SerialPlacement.Top;

    private int tentative;

    public event ReadingChangedEventHandler ReadingChanged;

    public static SerialUSBIMU GetDefault()
    {
        if (serial.port.IsOpen)
            return serial;

        USBDeviceInfo deviceInfo = null;
        var devices = GetSerialDevices();

        foreach (var sensor in vendors)
        {
            var VendorID = sensor.Key.Key;
            var ProductID = sensor.Key.Value;

            deviceInfo = devices.FirstOrDefault(a => a.VID == VendorID && a.PID == ProductID);
            if (deviceInfo is not null)
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
        return device is not null ? device.Name : "N/A";
    }

    public double GetFilterCutoff()
    {
        return port.oneEuroSettings.minCutoff;
    }

    public double GetFilterBeta()
    {
        return port.oneEuroSettings.beta;
    }

    public async void Open()
    {
        tentative = 0; // reset tentative

        LogManager.LogInformation("{0} connecting to {1}", serial.ToString(), serial.device.Name);

        while (!serial.port.IsOpen && tentative < maxTentative)
            try
            {
                serial.port.Open();
                serial.port.DataReceived += serial.DataReceivedHandler;

                LogManager.LogInformation("{0} connected", serial.ToString());
            }
            catch
            {
                // port is not ready yet
                tentative++;
                LogManager.LogError("{0} could not connect. Attempt: {1} out of {2}", serial.ToString(), tentative,
                    maxTentative);
                await Task.Delay(500);
            }
    }

    public bool Close()
    {
        try
        {
            serial.port.Close();
            serial.port.DataReceived -= serial.DataReceivedHandler;

            LogManager.LogInformation("{0} disconnected", serial.ToString());
            return true;
        }
        catch
        {
            return false;
        }
    }

    // When data is received over the serial port, parse.	
    private async void DataReceivedHandler(object sender, SerialDataReceivedEventArgs e)
    {
        var index = 0;
        ushort usLength;
        var byteTemp = new byte[1000];

        try
        {
            // Read serial, store in byte array, at specified offset, certain amount and determine length
            usLength = (ushort)port.Read(byteTemp, 0, 1000);
        }
        catch
        {
            return;
        }

        // Default output mode is continues
        // Check frame header ID (default is 0xA4) and update rate 0x03 (default is 100 Hz 0x03)
        if (byteTemp[index] == 0xA4 && byteTemp[index + 1] == 0x03)
        {
            var datalength = 5 + byteTemp[index + 3];

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
                byte[] buffer = { 0xA4, 0x03, 0x08, 0x12, 0xC1 };

                LogManager.LogError("Serial USB Received unexpected datalength and start register, setting register..");

                try
                {
                    port.Write(buffer, 0, buffer.Length);
                }
                catch
                {
                    return;
                }

                return;
            }

            // Determination calibration
            if (openAutoCalib)
            {
                // Initialization
                // Address write function code register = 0xA4, 0x03
                // Register to read/write 0x07 Status query
                // Data checksum lower 8 bits  0x10
                byte[] buffer = { 0xA4, 0x06, 0x07, 0x5F, 0x10 };

                LogManager.LogInformation("Serial USB Calibrating Sensor");

                try
                {
                    port.Write(buffer, 0, buffer.Length);
                }
                catch
                {
                    return;
                }

                await Task.Delay(100);

                // Address write function code register = 0xA4, 0x03
                // Register to read/write save settings 0x05
                // 0x55 save current configuration
                buffer = new byte[] { 0xA4, 0x06, 0x05, 0x55, 0x04 };

                LogManager.LogInformation("Serial USB save settings on device");

                port.Write(buffer, 0, buffer.Length);
                openAutoCalib = false;
            }

            var array = new byte[datalength];
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
        var IntData = new short[9];

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

    public void PlacementTransformation(SerialPlacement SensorPlacement, bool isUpsideDown)
    {
        // Adaption of XYZ or invert based on USB port location on device. 
        // Upsidedown option in case of USB-C port usage or unusual USB-A installation. Pins on screen side is default.

        var AccTemp = AccelerationG;
        var AngVelTemp = AngularVelocityDeg;

        switch (SensorPlacement)
        {
            case SerialPlacement.Top:
            {
                AccelerationG.X = -AccTemp.X;

                if (isUpsideDown)
                {
                    AccelerationG.X = AccTemp.X; // Intentionally undo previous
                    AccelerationG.Y = -AccTemp.Y;

                    AngularVelocityDeg.X = -AngVelTemp.X;
                    AngularVelocityDeg.Y = -AngVelTemp.Y;
                }
            }
                break;
            case SerialPlacement.Right:
            {
                AccelerationG.X = AccTemp.Z;
                AccelerationG.Z = AccTemp.X;

                AngularVelocityDeg.X = -AngVelTemp.Z;
                AngularVelocityDeg.Z = AngVelTemp.X;

                if (isUpsideDown)
                {
                    AccelerationG.Y = -AccTemp.Y;
                    AccelerationG.Z = -AccTemp.X;

                    AngularVelocityDeg.Y = -AngVelTemp.Y;
                    AngularVelocityDeg.Z = -AngVelTemp.X;
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
                    AccelerationG.X = -AccTemp.X;
                    AccelerationG.Y = -AccTemp.Y;

                    AngularVelocityDeg.X = AngVelTemp.X; // Intentionally undo previous
                    AngularVelocityDeg.Y = -AngVelTemp.Y;
                }
            }
                break;
            case SerialPlacement.Left:
            {
                AccelerationG.X = -AccTemp.Z;
                AccelerationG.Z = -AccTemp.X;

                AngularVelocityDeg.X = AngVelTemp.Z;
                AngularVelocityDeg.Z = -AngVelTemp.X;

                if (isUpsideDown)
                {
                    AccelerationG.Y = -AccTemp.Y;
                    AccelerationG.Z = AccTemp.X;

                    AngularVelocityDeg.Y = -AngVelTemp.Y;
                    AngularVelocityDeg.Z = AngVelTemp.X;
                }
            }
                break;
        }
    }

    public void SetSensorPlacement(SerialPlacement SensorPlacement, bool isUpsideDown)
    {
        this.SensorPlacement = SensorPlacement;
        this.isUpsideDown = isUpsideDown;
    }
}