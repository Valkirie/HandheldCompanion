using System.IO.Ports;
using static ControllerCommon.OneEuroFilter;

namespace ControllerCommon.Sensors;

public class SerialPortEx : SerialPort
{
    public OneEuroSettings oneEuroSettings;
}