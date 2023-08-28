using System.IO.Ports;
using static HandheldCompanion.OneEuroFilter;

namespace HandheldCompanion.Sensors;

public class SerialPortEx : SerialPort
{
    public OneEuroSettings oneEuroSettings;
}