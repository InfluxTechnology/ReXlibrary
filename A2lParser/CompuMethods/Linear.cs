using System;
using System.Collections.Generic;
using System.Text;

namespace A2lParserLib.CompuMethods
{
    internal class Linear : CompuMethod
    {
        public double Factor { get; set; }
        public double Offset { get; set; }
        public Linear()
        {
            Type = Enums.CompuMethodType.LINEAR;
        }
    }
}
