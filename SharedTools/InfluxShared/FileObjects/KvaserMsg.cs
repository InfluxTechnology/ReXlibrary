using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace InfluxShared.FileObjects
{
    internal class KvaserMsg
    {
        public double TimeSeconds { get; set; }     // e.g. 2.881960
        public int Channel { get; set; }             // e.g. 1
        public uint Identifier { get; set; }         // e.g. 0x130
        public string Direction { get; set; } = "Rx"; // Rx or Tx
        public int DLC { get; set; }                 // 0–8
        public byte[] Data { get; set; } = new byte[8];
        public int Counter { get; set; }             // incrementing

        
    }

}
