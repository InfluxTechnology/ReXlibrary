using System;
using System.Collections.Generic;
using System.Text;
using static A2lParserLib.Enums;

namespace A2lParserLib.CompuMethods
{
    internal class CompuTable : CompuMethod
    {
        public int NumberOfPairs { get; set; }
        public Dictionary<Int64, object> Values { get; set; }
    }
}
