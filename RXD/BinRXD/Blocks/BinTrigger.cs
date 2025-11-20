using InfluxShared.FileObjects;
using System;

namespace RXD.Blocks
{
    #region Enumerations for Property type definitions

    #endregion

    internal class BinTrigger : BinBase
    {
        internal enum BinProp
        {
            KeepActiveTime,
            DoNotActivateTimeout,
            InputUID1,
            InputUID2,
            OperatorCondition,
            NameSize,
            Name,
            ConditionAcceptanceTime,
            MaximumTriggeringCount,
            TriggerCountResetUID,
            ImpulseOnTrue,
            InputUID3
        }

        #region Do not touch these
        public BinTrigger(BinHeader hs = null) : base(hs) { }

        internal dynamic this[BinProp index]
        {
            get => data.GetProperty(index.ToString());
            set => data.SetProperty(index.ToString(), value);
        }
        #endregion

        public override ChannelDescriptor GetDataDescriptor => new ChannelDescriptor()
        { StartBit = 0, BitCount = 8, isIntel = true, HexType = typeof(UInt16), conversionType = ConversionType.None, Name = GetName, Units = GetUnits };

        internal override void SetupVersions()
        {
            Versions[1] = new Action(() =>
            {
                data.AddProperty(BinProp.KeepActiveTime, typeof(UInt32));
                data.AddProperty(BinProp.DoNotActivateTimeout, typeof(UInt32));
                data.AddProperty(BinProp.InputUID1, typeof(UInt16));
                data.AddProperty(BinProp.InputUID2, typeof(UInt16));
                data.AddProperty(BinProp.OperatorCondition, typeof(ConditionType));
                AddInput(BinProp.InputUID1.ToString());
                AddInput(BinProp.InputUID2.ToString());
                AddOutput("UID");
            });
            Versions[2] = new Action(() =>
            {
                Versions[1].DynamicInvoke();
                data.AddProperty(BinProp.NameSize, typeof(byte));
                data.AddProperty(BinProp.Name, typeof(string), BinProp.NameSize);
            });
            Versions[3] = new Action(() =>
            {
                Versions[2].DynamicInvoke();
                data.AddProperty(BinProp.ConditionAcceptanceTime, typeof(UInt32));
                data.AddProperty(BinProp.MaximumTriggeringCount, typeof(UInt16));
                data.AddProperty(BinProp.TriggerCountResetUID, typeof(UInt16));
                data.AddProperty(BinProp.ImpulseOnTrue, typeof(bool));
            });
            Versions[4] = new Action(() =>
            {
                Versions[3].DynamicInvoke();
                data.AddProperty(BinProp.InputUID3, typeof(UInt16));
                AddInput(BinProp.InputUID3.ToString());
            });
        }

    }
}
