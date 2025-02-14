namespace HandheldCompanion.Devices.Valve
{
    public struct DeviceVersion
    {
        public ushort Firmware { get; set; }
        public byte BoardID { get; set; }
        public byte PDCS { get; set; }

        public bool BatteryTempLE { get; set; }
        public bool MaxBatteryCharge { get; set; }

        public bool IsSupported(ushort deviceFirmware, byte deviceBoardID, byte devicePDCS)
        {
            if (Firmware != 0 && Firmware != deviceFirmware)
                return false;
            if (BoardID != 0 && BoardID != deviceBoardID)
                return false;
            if (PDCS != 0 && PDCS != devicePDCS)
                return false;
            return true;
        }
    };
}
