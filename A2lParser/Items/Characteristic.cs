using A2lParserLib.CompuMethods;
using System;
using System.Collections.Generic;
using System.Text;
using static A2lParserLib.Enums;

namespace A2lParserLib.Items
{
    public class Characteristic : IA2lItem
    {
        public string Name { get; set; } = "";
        public string LongIdentifier { get; set; } = "";
        public string Description { get; set; } = "";
        public string Units { get; set; } = "";
        public uint EcuAddress { get; set; }
        public DataType DataType { get; set; }
        public ByteOrder ByteOrder { get; set; } = ByteOrder.Intel;
        public double MinValue { get; set; }
        public double MaxValue { get; set; }
        public CompuMethod CompuMethod { get; set; }
        public string AddressHex { get => "0x" + EcuAddress.ToString("X8"); }
        public byte Daq { get; set; }
        public ushort Size { get; set; }
    }
}
