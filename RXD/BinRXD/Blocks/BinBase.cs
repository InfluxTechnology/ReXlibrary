using InfluxShared.FileObjects;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;

namespace RXD.Blocks
{
    public abstract partial class BinBase
    {
        public enum BlockSubType { None, MessageTrigger, MessageFilter, MessageLogAll, MessageJ1939Filter, MessageJ1939DM, MessageDBC };

        internal Dictionary<UInt16, Delegate> Versions = new Dictionary<ushort, Delegate>();

        internal BinHeader header;

        internal PropertyCollection data = new PropertyCollection();

        internal PropertyCollection external = new PropertyCollection();

        /*internal dynamic this[Enum index]
        {
            get => data.GetProperty(index.ToString());
            set => data.SetProperty(index.ToString(), value);
        }*/

        internal BlockType BinType => header.type;

        public RecordType RecType = RecordType.Unknown;
        public BlockSubType SubType;
        public string Name => GetName;
        public string Units => GetUnits;
        public ChannelDescriptor DataDescriptor => GetDataDescriptor;
        public virtual string GetName => data.TryGetValue("name", out PropertyData name) ? name.Value : (ToString() + header.uniqueid);
        public virtual string GetUnits => data.TryGetValue("units", out PropertyData units) ? units.Value : "";
        internal virtual Guid GetGUID => data.TryGetValue("guid", out PropertyData guid) ? guid.Value : null;
        public virtual ChannelDescriptor GetDataDescriptor => null;
        internal UInt32 FirstTimestamp = 0;
        internal UInt32 LastTimestamp = 0;
        internal bool TimeOverlap = false;
        internal bool DataFound = false; // Used for validate detected lowest timestamp
        internal bool AddOverlap = false;
        internal virtual List<string> Inputs { get; set; } = new List<string>();
        internal virtual List<string> Outputs { get; set; } = new List<string>();

        public void AddInput(string prop) => Inputs.Add(prop);

        public void AddOutput(string prop) => Outputs.Add(prop);

        public BinBase(BinHeader hs = null)
        {
            SubType = BlockSubType.None;
            SetupVersions();

            if (hs is null)
                header = new BinHeader { type = BlockInfo.FirstOrDefault(x => x.Value == GetType()).Key };
            else
                header = hs;

            RecType = RecordInfo[BinType];
            InvokeVersion();
        }

        internal abstract void SetupVersions();

        void InvokeVersion()
        {
            if (Versions.Count == 0)
                return;

            UInt16 FirstVersion = Versions.Keys.First();
            UInt16 LastVersion = Versions.Keys.Last();

            if (header.version == 0 || header.version > LastVersion)
                header.version = LastVersion;

            if (header.version < FirstVersion)
                return;

            if (!Versions.ContainsKey(header.version))
                header.version = Versions.Where(x => x.Key < header.version).OrderByDescending(x => x.Key).First().Key;

            Versions[header.version].DynamicInvoke();
        }

        internal byte[] ToBytes()
        {
            UInt16 hdrSize = (UInt16)Marshal.SizeOf(typeof(BinHeader));
            if (BinType == BlockType.Config)
                if (header.version < 4)
                    hdrSize -= 2;

            byte[] propbytes = data.ToBytes();
            header.length = (UInt16)(hdrSize + propbytes.Length + 1);

            byte[] buffer = new byte[header.length];
            GCHandle h = GCHandle.Alloc(buffer, GCHandleType.Pinned);
            IntPtr p = h.AddrOfPinnedObject();
            Marshal.StructureToPtr(header, p, false);
            h.Free();

            if (propbytes.Length > 0)
                Array.Copy(propbytes, 0, buffer, hdrSize, propbytes.Length);

            // Checksum update
            buffer[buffer.Length - 1] = 0;
            byte crc = 0;
            ChecksumUpdate(buffer, ref crc);
            buffer[buffer.Length - 1] = crc;

            return buffer;
        }
    }
}
