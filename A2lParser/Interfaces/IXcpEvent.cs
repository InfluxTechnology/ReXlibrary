using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;

namespace A2lParserLib.Interfaces
{
    public interface IXcpEvent
    {
        public string Name { get; set; }
        public string ShortName { get; set; }
        public byte Channel { get; set; }
        public int MaxDaqList { get; set; } //Maximum number of DAQ lists in this event channel
        public byte TimeCycle { get; set; } //Event channel time cycle
        public byte TimeUnit { get; set; } //Event channel time unit
    }
}
