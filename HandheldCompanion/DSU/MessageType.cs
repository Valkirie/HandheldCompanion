namespace HandheldCompanion.DSU
{
    public enum MessageType
    {
        DSUC_VersionReq = 0x100000,
        DSUS_VersionRsp = 0x100000,
        DSUC_ListPorts = 0x100001,
        DSUS_PortInfo = 0x100001,
        DSUC_PadDataReq = 0x100002,
        DSUS_PadDataRsp = 0x100002
    }
}
