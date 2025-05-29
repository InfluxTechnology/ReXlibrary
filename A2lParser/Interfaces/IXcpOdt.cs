using System;
using System.Collections.Generic;
using System.Text;

namespace A2lParserLib.Interfaces
{
    public interface IXcpOdt
    {
        public ushort FilledSize { get; set; }
        public List<uint> Items { get; set; }
    }
}
