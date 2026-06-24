using InfluxShared.FileObjects;
using System;

namespace RXD.Blocks
{
    public class BinIMUConfiguration: BinBase
    {
        public enum Enclosure_Face_Type : byte
        {
            DEFAULT,
            FRONT,
            BACK,
            TOP,
            BOTTOM,
            LEFT,
            RIGHT
        }

        public enum IMU_Coordination_Frame : byte
        {
            FLU,
            FRD,
        }

        internal enum BinProp
        {
            PhysicalNumber,
            IMUXPlusFace,
            IMUZPlusFace,
            UseManualOffset,
            RollManualOffset,
            PitchManualOffset,
            YawManualOffset,
            UseManualGyroBiasCompensation,
            GyroXBias,
            GyroYBias,
            GyroZBias,
            CoordinationFrame
        }

        #region Do not touch these
        public BinIMUConfiguration(BinHeader hs = null) : base(hs) { }

        internal dynamic this[BinProp index]
        {
            get => data.GetProperty(index.ToString());
            set => data.SetProperty(index.ToString(), value);
        }
        #endregion

        //public override string GetName => $"Gyroscope {this[BinProp.Axis]}";
        //public override string GetUnits => "dps";
        public override ChannelDescriptor GetDataDescriptor => new ChannelDescriptor()
        { StartBit = 0, BitCount = 32, isIntel = true, HexType = typeof(Single), conversionType = ConversionType.None, Name = GetName, Units = GetUnits };

        internal override void SetupVersions()
        {
            Versions[1] = new Action(() =>
            {
                data.AddProperty(BinProp.PhysicalNumber, typeof(byte));
                data.AddProperty(BinProp.IMUXPlusFace, typeof(Enclosure_Face_Type));
                data.AddProperty(BinProp.IMUZPlusFace, typeof(Enclosure_Face_Type));
                data.AddProperty(BinProp.UseManualOffset, typeof(bool));
                data.AddProperty(BinProp.RollManualOffset, typeof(Single));
                data.AddProperty(BinProp.PitchManualOffset, typeof(Single));
                data.AddProperty(BinProp.YawManualOffset, typeof(Single));
                data.AddProperty(BinProp.UseManualGyroBiasCompensation, typeof(bool));
                data.AddProperty(BinProp.GyroXBias, typeof(Single));
                data.AddProperty(BinProp.GyroYBias, typeof(Single));
                data.AddProperty(BinProp.GyroZBias, typeof(Single));
                data.AddProperty(BinProp.CoordinationFrame, typeof(IMU_Coordination_Frame));

                AddOutput("UID");
            });
        }
    }
}
