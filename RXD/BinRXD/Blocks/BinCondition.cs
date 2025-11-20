using System;

namespace RXD.Blocks
{
    #region Enumerations for Property type definitions
    public enum ConditionType : byte
    {
        EQUAL,
        GREATER,
        LESS,
        EQUAL_GREATER,
        EQUAL_LESS,
        NOT_EQUAL,
        NEW,
        INCREMENT,
        DECREMENT,
        CHANGE,
        SAME,
        IN_RANGE,
        NOT_IN_RANGE,
        IN_RANGE_INCLUSIVE,
        NOT_IN_RANGE_INCLUSIVE
    }
    #endregion

    internal class BinCondition : BinBase
    {
        internal enum BinProp
        {
            Input1UID,
            Input2UID,
            InputConditionUID1,
            InputConditionUID2,
            OperatorCondition,
        }

        #region Do not touch these
        public BinCondition(BinHeader hs = null) : base(hs) { }

        internal dynamic this[BinProp index]
        {
            get => data.GetProperty(index.ToString());
            set => data.SetProperty(index.ToString(), value);
        }
        #endregion

        internal override void SetupVersions()
        {
            Versions[1] = new Action(() =>
            {
                data.AddProperty(BinProp.Input1UID, typeof(UInt16));
                data.AddProperty(BinProp.Input2UID, typeof(UInt16));
                data.AddProperty(BinProp.InputConditionUID1, typeof(UInt16));
                data.AddProperty(BinProp.InputConditionUID2, typeof(UInt16));
                data.AddProperty(BinProp.OperatorCondition, typeof(ConditionType));
            });
            AddInput(BinProp.Input1UID.ToString());
            AddInput(BinProp.Input2UID.ToString());
            AddInput(BinProp.InputConditionUID1.ToString());
            AddInput(BinProp.InputConditionUID2.ToString());
            AddOutput("UID");
        }
    }
}