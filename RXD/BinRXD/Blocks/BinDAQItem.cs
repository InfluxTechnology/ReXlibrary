using System;

namespace RXD.Blocks
{
    internal class BinDAQItem : BinBase
    {
        internal enum BinProp
        {
            DAQID,
            NameSize,
            Name,
            InputUID,
        }


        #region Do not touch these
        public BinDAQItem(BinHeader hs = null) : base(hs) { }
        public BinDAQItem(string name, ushort daqId, ushort inputUid, BinHeader hs = null) : base(hs) 
        { 
            this[BinProp.Name] = name;
            this[BinProp.NameSize] = (byte)name.Length;
            this[BinProp.DAQID] = daqId;
            this[BinProp.InputUID] = inputUid;
        }

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
                data.AddProperty(BinProp.DAQID, typeof(UInt16));
                data.AddProperty(BinProp.NameSize, typeof(byte));
                data.AddProperty(BinProp.Name, typeof(string), BinProp.NameSize);
                data.AddProperty(BinProp.InputUID, typeof(UInt16));
            });

            AddInput(BinProp.InputUID.ToString());
            AddOutput("UID");
            AddOutput(BinProp.DAQID.ToString());
        }

    }
}
