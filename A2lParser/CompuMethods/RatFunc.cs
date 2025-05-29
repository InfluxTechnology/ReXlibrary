using A2lParserLib.CompuMethods;
using System;
using System.Collections.Generic;
using System.Text;

namespace A2lParserLib.CompuMethods
{
    internal class RatFunc : CompuMethod
    {
        public double[] Coeffs { get; set; } = new double[] {0,1,0,0,0,1 };
        public double Factor { get => GetFactor(); }

        private double GetFactor()
        {
            return Coeffs[1];
        }

        public double Offset { get => GetOffset(); }

        private double GetOffset()
        {
            return Coeffs[2];
        }
    }
}
