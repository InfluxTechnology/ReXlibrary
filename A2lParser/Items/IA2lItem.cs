using A2lParserLib.CompuMethods;
using System;
using System.Collections.Generic;
using System.Text;
using static A2lParserLib.Enums;

namespace A2lParserLib.Items
{
    

    public interface IA2lItem
    {
        string Name { get; set; }
        string LongIdentifier { get; set; }
        string Description { get; set; }
        string Units { get; set; }
        uint EcuAddress { get; set; }
        string AddressHex { get; }
        DataType DataType { get; set; }
        ByteOrder ByteOrder { get; set; }
        double MinValue { get; set; }
        double MaxValue { get; set; }
        byte Daq { get; set; }
        ushort Size { get;set; }
        CompuMethod CompuMethod { get; set; }
    }
}
