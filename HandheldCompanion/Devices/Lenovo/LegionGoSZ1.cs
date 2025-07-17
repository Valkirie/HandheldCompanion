namespace HandheldCompanion.Devices
{
    public class LegionGoSZ1 : LegionGo
    {
        public LegionGoSZ1()
        {
            // used to monitor OEM specific inputs
            vendorId = 0x1A86;
            productIds = [
                0xE310, // xinput
                0xE311, // dinput
            ];
            hidFilters = new()
            {
                { 0xE310, new HidFilter(unchecked((short)0xFFA0), unchecked((short)0x0001)) }, // xinput
                { 0xE311, new HidFilter(unchecked((short)0xFFA0), unchecked((short)0x0001)) }, // dinput
            };
        }
    }
}
