namespace HandheldCompanion.Devices.MSI
{
    public struct DeviceVersion
    {
        public int Firmware { get; set; }
        public byte[] RGB { get; set; }
        public byte[] M1DInput { get; set; }
        public byte[] M2DInput { get; set; }
        public byte[]? M1XInput { get; set; }
        public byte[]? M2XInput { get; set; }

        public bool IsSupported(int firmware)
        {
            return firmware == Firmware;
        }
    };
}
