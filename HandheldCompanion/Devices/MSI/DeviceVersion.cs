namespace HandheldCompanion.Devices.MSI
{
    public struct DeviceVersion
    {
        public int Firmware { get; set; }
        public byte[] RGB { get; set; }
        public byte[] M1 { get; set; }
        public byte[] M2 { get; set; }

        public bool IsSupported(int firmware)
        {
            return firmware == Firmware;
        }
    };
}
