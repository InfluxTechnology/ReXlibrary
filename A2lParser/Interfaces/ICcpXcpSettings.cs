using A2lParserLib.Settings;
using System.Collections.Generic;
using static A2lParserLib.Enums;

namespace A2lParserLib.Interfaces
{
    public interface ICcpXcpSettings
    {
        public string Name { get; set; }
        public uint Cro { get; set; }
        public uint Dto { get; set; }
        public ushort StationAddress { get; set; }
        public uint MaxDaq { get; set; }  //Total number of available DAQ lists
        public uint MaxEventChannels { get; set; }  //Total number of available event channels
        public byte MinDaq { get; set; } //Total number of predefined DAQ lists
        public DaqType DaqType { get; set; } //The flag indicates whether the DAQ lists that are not PREDEFINED shall be configured statically or dynamically
        public ByteOrder ByteOrder { get; set; }
        public uint Baudrate { get; set; }
        public uint BaudrateFD { get; set; }
        public byte RateIndex { get; set; }
        public ushort OdtSize { get; set; }
        public ushort OdtEntrySize { get; set; }
        public bool IsExtended { get; set; }
        public bool IsCanFd { get; set; }
        public XcpTimestamp Timestamp { get; set; }
        public XcpTimestampResolution TimestampResolution { get; set; }
        public List<XcpDaq> Daqs { get; set; }
        public List<XcpEvent> Events { get; set; }
        public List<string> Cmmds { get; set; }
        public bool IsXcp { get; set; } 
        public bool SynchStartDaqChannels { get; set; }
        public bool UseSeedKey { get; set; }
        public string SeedFileCal { get; set; }
        public string SeedFileDaq { get; set; }
        public string SeedFilePgm { get; set; }
        public string SeedFileStim { get; set; }
    }
}
