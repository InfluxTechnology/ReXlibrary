﻿using System;

namespace RXD.Blocks
{
    internal class BinDAQ : BinBase
    {
        internal enum BinProp
        {
            Number,
            SamplingRate,
        }

        #region Do not touch these
        public BinDAQ(BinHeader hs = null) : base(hs) { }

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
                data.AddProperty(BinProp.Number, typeof(UInt16));
                data.AddProperty(BinProp.SamplingRate, typeof(UInt32));
            });

            AddInput("UID");
            AddOutput("UID");
        }
    }
}
