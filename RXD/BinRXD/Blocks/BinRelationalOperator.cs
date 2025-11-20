using System;
using System.Collections.Generic;
using System.Text;

namespace RXD.Blocks
{
    internal class BinRelationalOperator: BinBase
    {
        internal enum BinProp
        {
            InputUID1,
            InputUID2,
            RelationalOperator
        }

        #region Do not touch these
        public BinRelationalOperator(BinHeader hs = null) : base(hs) { }

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
                data.AddProperty(BinProp.InputUID1, typeof(UInt16));
                data.AddProperty(BinProp.InputUID2, typeof(UInt16));
                data.AddProperty(BinProp.RelationalOperator, typeof(ConditionType));
            });            

            AddInput(BinProp.InputUID1.ToString());
            AddInput(BinProp.InputUID2.ToString());
            AddOutput("UID");
        }
    }
}
