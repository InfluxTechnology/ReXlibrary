using A2lParserLib.Interfaces;
using System;
using System.Collections.Generic;
using System.Text;

namespace A2lParserLib.Settings
{
    public class Odt: IOdt
    {
        public ushort FilledSize { get; set; } = 0;
        public List<uint> Items { get; set; } = new();
    }
}
