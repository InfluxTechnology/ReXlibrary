using InfluxShared.FileObjects;
using System;

namespace RXD.Blocks
{
    public class BinInternalParameter : BinBase
    {
        public enum Parameter_Type : byte
        {
            Assembly_Number,
            Batch_Number,
            Config_Version_0_3,
            Config_Version_12_15,
            Config_Version_4_7,
            Config_Version_8_11,
            FW_Branch,
            FW_Major,
            FW_Minor,
            FW_Type,
            Product_Number,
            RTC,
            Serial_Number,
            TimeStamp,
            Mobile_Loop_Status,
            IMU_Temperature,
            MCU_Temperature,
        }

        public enum Value_Type : byte
        {
            UBYTE,
            SBYTE,
            UWORD,
            SWORD,
            ULONG,
            SLONG,
            FLOAT32,
            FLOAT64,
            ULONGLONG,
            SLONGLONG
        }

        internal enum BinProp
        {
            Parameter_Type,
            Value_Type,
            SamplingRate,
            InputUID
        }


        #region Do not touch these
        public BinInternalParameter(BinHeader hs = null) : base(hs) { }

        internal dynamic this[BinProp index]
        {
            get => data.GetProperty(index.ToString());
            set => data.SetProperty(index.ToString(), value);
        }
        #endregion

        public override string GetName => $"IntParam_{this[BinProp.Parameter_Type]}";
        public override string GetUnits => "";
        public override ChannelDescriptor GetDataDescriptor => new ChannelDescriptor()
        { StartBit = 0, BitCount = 32, isIntel = true, HexType = typeof(Single), conversionType = ConversionType.None, Name = GetName, Units = GetUnits };

        internal override void SetupVersions()
        {

            Versions[1] = new Action(() =>
            {
                data.AddProperty(BinProp.Parameter_Type, typeof(Parameter_Type));
                data.AddProperty(BinProp.Value_Type, typeof(Value_Type));
                data.AddProperty(BinProp.SamplingRate, typeof(UInt16));
            });
            Versions[2] = new Action(() =>
            {
                Versions[1].DynamicInvoke();
                data.AddProperty(BinProp.InputUID, typeof(UInt16));
                AddInput(BinProp.InputUID.ToString());
                AddOutput("UID");
            });
        }
    }
}
