using A2lParserLib.Settings;
using System;
using System.Collections.Generic;
using System.Text;

namespace A2lParserLib.Interfaces
{
    public interface IXcpDaq
    {
        public string Name { get; set; }
        public byte DaqIndex { get; set; }
        public uint Ident { get; set; }
        public ulong Sampling { get; set; } //in nanoseconds
        public string SamplingStr { get; set; }
        public byte MaxOdt { get; set; }  //Max Number of ODTs in this DAQ list. If 0 list is Dynamic
        public short FirstPid { get; set; }
        public byte MaxOdtEntries { get; set; }  //Maximum number of entries in an ODT
        public byte EventChannel { get; set; }
        public bool EventFixed { get; set; }
        public List<XcpOdt> Odts { get; set; }
    }
}
