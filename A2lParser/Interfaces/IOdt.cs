using System;
using System.Collections.Generic;
using System.Text;

namespace A2lParserLib.Interfaces
{
    public interface IOdt
    {
        public ushort FilledSize { get; set; }
        public List<uint> Items { get; set; }
    }
}
