using InfluxShared.FileObjects;
using System;

namespace RXD.Blocks
{
    public enum SignalOperationType : byte
    {
        DIFFERENCE,
        DERIVATIVE,
        INTEGRATOR,
        ACCUMULATOR
    }

    public class BinSignal_Operator : BinBase
    {
        internal enum BinProp
        {
            InputUID,
            Operator_Type,
            InitialValue,
            NameSize,
            Name,
        }

        #region Do not touch these
        public BinSignal_Operator(BinHeader hs = null) : base(hs) { }

        internal dynamic this[BinProp index]
        {
            get => data.GetProperty(index.ToString());
            set => data.SetProperty(index.ToString(), value);
        }
        #endregion
        
        //public override string GetName => "Operator " + this[BinSignal_Operator.BinProp.Operator_Type].ToString();
        public override ChannelDescriptor GetDataDescriptor => new ChannelDescriptor()
        { StartBit = 0, BitCount = 32, isIntel = true, HexType = typeof(Single), conversionType = ConversionType.None, Name = GetName, Units = GetUnits };

        internal override void SetupVersions()
        {
            Versions[1] = new Action(() =>
            {
                data.AddProperty(BinProp.InputUID, typeof(UInt16));
                data.AddProperty(BinProp.Operator_Type, typeof(SignalOperationType));
                data.AddProperty(BinProp.InitialValue, typeof(Single));
                data.AddProperty(BinProp.NameSize, typeof(byte));
                data.AddProperty(BinProp.Name, typeof(string), BinProp.NameSize);
            });

            AddInput(BinProp.InputUID.ToString());
            AddOutput("UID");
        }
    }
}
