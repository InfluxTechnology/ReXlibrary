using System;

namespace RXD.Blocks
{
    #region Enumerations for Property type definitions
    internal enum LogFormatType : byte
    {
        InfluxGeneric1
    }
    #endregion

    internal class BinSDInterface : BinBase
    {
        internal enum BinProp
        {
            MaxLogSize,
            MaxLogTime,
            LogFormat,
            //EnableUID,
            //DisableUID,
            EnableUIDCount,
            DisableUIDCount,
            EnableUIDs,
            DisableUIDs,
            InitialEnableState,
            IsEnableCreateNewLog,
            IsPostTimeFromEnableStart,
            NumberOfLogs,
            PostLogTime,
            PreLogTime,
            PartitionID,
            PostLogOnlyOnEnable,
            PostLogContinuous
        }

        #region Do not touch these
        public BinSDInterface(BinHeader hs = null) : base(hs) { }

        internal dynamic this[BinProp index]
        {
            get => data.GetProperty(index.ToString());
            set => data.SetProperty(index.ToString(), value);
        }
        #endregion

        internal override void SetupVersions()
        {
            Versions[2] = new Action(() =>
            {
                data.AddProperty(BinProp.MaxLogSize, typeof(UInt32));
                data.AddProperty(BinProp.MaxLogTime, typeof(UInt32));
                data.AddProperty(BinProp.LogFormat, typeof(LogFormatType));
            });
            Versions[3] = new Action(() =>
            {
                Versions[2].DynamicInvoke();
                data.AddProperty(BinProp.PreLogTime, typeof(UInt32));
                data.AddProperty(BinProp.PostLogTime, typeof(UInt32));
                data.AddProperty(BinProp.IsPostTimeFromEnableStart, typeof(bool));
                data.AddProperty(BinProp.NumberOfLogs, typeof(UInt32));
                data.AddProperty(BinProp.IsEnableCreateNewLog, typeof(bool));
                data.AddProperty(BinProp.InitialEnableState, typeof(bool));
                //data.AddProperty(BinProp.EnableUID, typeof(UInt16));
                //data.AddProperty(BinProp.DisableUID, typeof(UInt16));
                if (header.version > 5)
                {
                    data.AddProperty(BinProp.EnableUIDCount, typeof(byte));
                    data.AddProperty(BinProp.DisableUIDCount, typeof(byte));
                    data.AddProperty(BinProp.EnableUIDs, typeof(UInt16[]), BinProp.EnableUIDCount);
                    data.Property(BinProp.EnableUIDs).XmlSequenceGroup = "EnableUID";
                    data.Property(BinProp.EnableUIDs).Name = "EnableUID";
                    data.AddProperty(BinProp.DisableUIDs, typeof(UInt16[]), BinProp.DisableUIDCount);
                    data.Property(BinProp.DisableUIDs).XmlSequenceGroup = "DisableUID";
                    data.Property(BinProp.DisableUIDs).Name = "DisableUID";
                }
                AddInput("EnableUID");
                AddInput("DisableUID");
            });
            Versions[4] = new Action(() =>
            {
                Versions[3].DynamicInvoke();
                data.AddProperty(BinProp.PartitionID, typeof(byte));
            });
            Versions[5] = new Action(() =>
            {
                Versions[4].DynamicInvoke();
                data.AddProperty(BinProp.PostLogOnlyOnEnable, typeof(bool));
                data.AddProperty(BinProp.PostLogContinuous, typeof(bool));
            });
            Versions[6] = new Action(() =>
            {
                Versions[5].DynamicInvoke();
                // added in Version 3 check to modify properties
            });
            AddInput("");
        }

    }
}
