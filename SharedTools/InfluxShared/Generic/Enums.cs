namespace InfluxShared.Generic
{
    public enum CanFDMessageType : byte
    {
        NORMAL_CAN,
        FD_CAN,
        FD_FAST_CAN,
    }

    public enum Protocol_Type : byte
    {
        NO,
        UDS,
        XCP
    }

    public enum RealTimeSourceType : byte
    {
        No,
        Mobile,
        NTP
    }

    public enum RemoteStorageType : byte
    {
        FTP,
        S3
    }
}
