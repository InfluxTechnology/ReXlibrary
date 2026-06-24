using System;
using System.Collections.Generic;
using System.Text;

namespace RXD.Blocks
{
    internal class BinXCP : BinBase
    {
        internal enum BinProp
        {
            NameSize,
            Name,
            TesterMessageUID,
            ModuleMessageUID,
            FirstMessageUID,
            TesterPresentTime,
            DAQMessageCount,
            DAQMessageUID,
            NoResponseTimeout,
            SeedKeyCount,
            SeedMessageUID,
            KeyMessageUID,
            UnlockFileNameSize,
            UnlockFileName
        }

        #region Do not touch these
        public BinXCP(BinHeader hs = null) : base(hs) { }

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
                data.AddProperty(BinProp.NameSize, typeof(byte));
                data.AddProperty(BinProp.Name, typeof(string), BinProp.NameSize);
                data.AddProperty(BinProp.TesterMessageUID, typeof(ushort));
                data.AddProperty(BinProp.ModuleMessageUID, typeof(ushort));
                data.AddProperty(BinProp.FirstMessageUID, typeof(ushort));
                data.AddProperty(BinProp.TesterPresentTime, typeof(ushort));
                data.AddProperty(BinProp.DAQMessageCount, typeof(byte));
                data.AddProperty(BinProp.DAQMessageUID, typeof(ushort[]), BinProp.DAQMessageCount);
                data.AddProperty(BinProp.NoResponseTimeout, typeof(ushort), 1000);
                data.Property(BinProp.DAQMessageUID).XmlSequenceGroup = "DAQMessageUID";                

                AddInput("UID");
                AddOutput(BinProp.FirstMessageUID.ToString());
            });
            Versions[2] = new Action(() =>
            {
                Versions[1].DynamicInvoke();
                data.AddProperty(BinProp.SeedKeyCount, typeof(byte));
                data.AddProperty(BinProp.SeedMessageUID, typeof(ushort[]), BinProp.SeedKeyCount);
                data.AddProperty(BinProp.KeyMessageUID, typeof(ushort[]), BinProp.SeedKeyCount);
                data.AddProperty(BinProp.UnlockFileNameSize, typeof(byte[]), BinProp.SeedKeyCount);
                data.AddProperty(BinProp.UnlockFileName, typeof(string[]), BinProp.SeedKeyCount);
                data.Property(BinProp.UnlockFileName).SubElementSizes = data.Property(BinProp.UnlockFileNameSize);
                data.Property(BinProp.SeedMessageUID).XmlSequenceGroup = "SEED_KEY";
                data.Property(BinProp.KeyMessageUID).XmlSequenceGroup = "SEED_KEY";
                data.Property(BinProp.UnlockFileName).XmlSequenceGroup = "SEED_KEY";

            });
        }
    }
}
