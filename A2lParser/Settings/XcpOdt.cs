using A2lParserLib.Interfaces;
using System;
using System.Collections.Generic;
using System.Text;

namespace A2lParserLib.Settings
{
    public class XcpOdt: IXcpOdt
    {
        public ushort FilledSize { get; set; } = 0;
        public List<uint> Items { get; set; } = new();
    }
}
