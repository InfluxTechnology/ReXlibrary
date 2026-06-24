using InfluxShared.FileObjects;
using System;

namespace RXD.Blocks
{
    public enum SignalFilterType : byte
    {
        MOVING_AVERAGE,
        MEDIAN,
        LOW_PASS,
        HIGH_PASS
    }

    public class BinSignal_Filter : BinBase
    {
        internal enum BinProp
        {
            InputUID,
            Filter_Type,
            InitialValue,
            WindowSize,
            Alpha,
            NameSize,
            Name,
        }

        #region Do not touch these
        public BinSignal_Filter(BinHeader hs = null) : base(hs) { }

        internal dynamic this[BinProp index]
        {
            get => data.GetProperty(index.ToString());
            set => data.SetProperty(index.ToString(), value);
        }
        #endregion

        public override ChannelDescriptor GetDataDescriptor => new ChannelDescriptor()
        { StartBit = 0, BitCount = 32, isIntel = true, HexType = typeof(Single), conversionType = ConversionType.None, Name = GetName, Units = GetUnits };

        internal override void SetupVersions()
        {
            Versions[1] = new Action(() =>
            {
                data.AddProperty(BinProp.InputUID, typeof(UInt16));
                data.AddProperty(BinProp.Filter_Type, typeof(SignalFilterType));
                data.AddProperty(BinProp.InitialValue, typeof(Single));
                data.AddProperty(BinProp.WindowSize, typeof(byte));
                data.AddProperty(BinProp.Alpha, typeof(Single));
                data.AddProperty(BinProp.NameSize, typeof(byte));
                data.AddProperty(BinProp.Name, typeof(string), BinProp.NameSize);
            });

            AddInput(BinProp.InputUID.ToString());
            AddOutput("UID");
        }
    }
}
