using System;
using System.Collections.Generic;
using System.Text;
using static A2lParserLib.Enums;

namespace A2lParserLib.CompuMethods
{
    public class CompuMethod
    { 
        public string Name { get; set; }
        public string Description { get; set; }
        public CompuMethodType Type { get; set; }
        public string FormatString { get; set; }
        public string Units { get; set; } = "";
    }
}
