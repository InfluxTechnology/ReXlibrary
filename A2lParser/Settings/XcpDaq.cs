using A2lParserLib.Interfaces;
using InfluxShared.Helpers;
using System.Collections.Generic;

namespace A2lParserLib.Settings
{
    public class XcpDaq : IXcpDaq
    {
        public string Name { get; set; } = "";
        public byte DaqIndex { get;set; }
        public uint Ident { get; set; }
        public ulong Sampling { get; set; } = 100000000;
        public string SamplingStr { get; set; } = "100 msec";
        public byte MaxOdt { get; set; } = 10;  //Max Number of ODTs in this DAQ list. If 0 list is Dynamic
        public short FirstPid { get; set; }
        public byte MaxOdtEntries { get; set; }  //Maximum number of entries in an ODT
        public byte EventChannel { get; set; }
        public string IdentHex
        {
            get => "0x" + Ident.ToString("X2");
            set => Ident = value.ConvertFromHex();
        }
        public bool EventFixed { get; set; }
        public List<XcpOdt> Odts { get; set; } = new ();
        public bool AddOdtItem(ushort itemSize, ushort odtSize, out ushort odtNum, out ushort startBit)
        {
            odtNum = 0;
            startBit = 0;
            for (int i = 0; i < Odts.Count; i++)
            {
                if (Odts[i].FilledSize + itemSize <= odtSize)
                {
                    startBit = (ushort)(Odts[i].FilledSize * 8 + 8);
                    Odts[i].FilledSize += itemSize;
                    odtNum = (ushort)i;
                    return true;
                }
            }            
            return false;
        }
    }
}
