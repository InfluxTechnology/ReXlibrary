using A2lParserLib.Interfaces;
using InfluxShared.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using static A2lParserLib.Enums;
using static A2lParserLib.Helpers;

namespace A2lParserLib.Settings
{
    public class XcpSettings : IXcpSettings
    {
        public string Name { get; set; } = "";
        public uint Cro { get; set; }
        public uint Dto { get; set; }
        public ushort StationAddress { get; set; }
        public string CroHex
        {
            get => "0x" + Cro.ToString("X2");
            set => Cro = value.ConvertFromHex();
        }
        public string DtoHex
        {
            get => "0x" + Dto.ToString("X2");
            set => Dto = value.ConvertFromHex();
        }
        public string StationAddressHex
        {
            get => "0x" + StationAddress.ToString("X2");
            set => StationAddress = (ushort)value.ConvertFromHex();
        }
        public uint MaxDaq {  get; set; }  //Total number of available DAQ lists
        public uint MaxEventChannels { get; set; }  //Total number of available event channels
        public byte MinDaq { get; set; } //Total number of predefined DAQ lists
        public DaqType DaqType { get; set; } //The flag indicates whether the DAQ lists that are not PREDEFINED shall be configured statically or dynamically
        public ByteOrder ByteOrder { get; set; } 
        public uint Baudrate { get; set; }
        public uint BaudrateFD { get; set; }
        public byte RateIndex { get => GetRateIndex(); set => SetRate(value); }
        public ushort OdtSize { get; set; } = 7;
        public ushort OdtEntrySize { get; set; }
        public List<XcpDaq> Daqs { get; set; } = new();
        public List<XcpEvent> Events { get; set; } = new();
        public List<string> Cmmds { get; set; }



        public XcpSettings()
        {
        }

        private byte GetRateIndex()
        {
            if (Baudrate < 126000)
                return 4;
            else if (Baudrate < 255000)
                return 3;
            else if (Baudrate < 501000)
                return 2;
            else if (Baudrate < 751000)
                return 1;
            else
                return 0;
        }

        private void SetRate(byte index)
        {
            if (index == 0)
                Baudrate = 1000000;
            else if (index == 1)
                Baudrate = 750000;
            else if (index == 2)
                Baudrate = 500000;
            else if (index == 3)
                Baudrate = 250000;
            else
                Baudrate = 125000;
        }

        public override string ToString()
        {
            return Name;
        }

        public void GetPrescaleFromEvents()
        {
            for (int i = 0; i < Daqs.Count; i++)
            {
                var daq = Daqs[i];
                if (daq.Name == "" && Events.Count > i)
                {
                    daq.Name = Events[i].Name;
                    daq.Sampling = Events[i].GetPrescale();
                    daq.SamplingStr = Events[i].GetPrescaleString();
                }
            }
        }

        internal byte GetDaqOdtCount(int daqIdx)
        {
            if (Daqs.Count > daqIdx)
            {
                return (byte)Daqs[daqIdx].Odts.Where(x => x.FilledSize > 0).Count();
            }
            return 0;
        }
    }
}
