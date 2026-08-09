namespace HandheldCompanion.Managers;

internal readonly struct ViiperDeviceHandle
{
    public readonly uint BusId;
    public readonly uint DeviceId;
    public readonly ushort VendorId;
    public readonly ushort ProductId;

    public ViiperDeviceHandle(uint busId, uint deviceId, ushort vendorId, ushort productId)
    {
        BusId = busId;
        DeviceId = deviceId;
        VendorId = vendorId;
        ProductId = productId;
    }
}
