using InfluxShared.FileObjects;
using System;
using System.Collections.Generic;
using System.Text;
using static RXD.Blocks.BinIMUConfiguration;

namespace RXD.Blocks
{
    public class BinOrientation : BinBase
    {
        public enum Orientation_Angle : byte
        {
            PITCH,
            ROLL,
            YAW
        }

        public enum Orientation_Calculation_Method_Type : byte
        {
            DEFAULT,
        }

        internal enum BinProp
        {
            PhysicalNumber,
            CalculationMethod,
            Angle,
            SamplingRate
        }

        #region Do not touch these
        public BinOrientation(BinHeader hs = null) : base(hs) { }

        internal dynamic this[BinProp index]
        {
            get => data.GetProperty(index.ToString());
            set => data.SetProperty(index.ToString(), value);
        }
        #endregion

        public override string GetName => $"Orientation {this[BinProp.Angle]}";
        //public override string GetUnits => "dps";

        public override ChannelDescriptor GetDataDescriptor => new ChannelDescriptor()
        { StartBit = 0, BitCount = 32, isIntel = true, HexType = typeof(Single), conversionType = ConversionType.None, Name = GetName, Units = GetUnits };

        internal override void SetupVersions()
        {
            Versions[1] = new Action(() =>
            {
                data.AddProperty(BinProp.PhysicalNumber, typeof(byte));
                data.AddProperty(BinProp.CalculationMethod, typeof(Orientation_Calculation_Method_Type));
                data.AddProperty(BinProp.Angle, typeof(Orientation_Angle));
                data.AddProperty(BinProp.SamplingRate, typeof(UInt16));

                AddOutput("UID");
            });
        }
    }
}
