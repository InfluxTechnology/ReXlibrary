using A2lParserLib.Interfaces;
using System;
using System.Collections.Generic;
using System.Text;

namespace A2lParserLib.Settings
{
    public class XcpEvent : IXcpEvent
    {
        public string Name {  get; set; }
        public string ShortName { get; set; }
        public byte Channel { get; set; }
        public int MaxDaqList { get; set; } = 10; //Maximum number of DAQ lists in this event channel
        public byte TimeCycle { get; set; } = 1; //Event channel time cycle
        public byte TimeUnit { get; set; } = 8; //Event channel time unit
        
    }
}
