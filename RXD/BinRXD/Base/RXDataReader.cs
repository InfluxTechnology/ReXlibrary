using InfluxShared.Generic;
using RXD.Base.FrameCollectors;
using RXD.Blocks;
using RXD.DataRecords;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace RXD.Base
{
    public enum ReadLogic
    {
        ReadData,
        UpdateLowestTimestamp,
        OffsetTimestamps,
        ReadPreBuffers,
    }

    public delegate bool AllowDebugInfo();

    internal class RXDataReader : IDisposable
    {
        public static string dbgRecordsSuffix = ".records";
        public static string dbgSectorsSuffix = ".sectors";
        public static string dbgAwsSuffix = ".aws";

        public static bool CreateDebugFiles = false;
        public static AllowDebugInfo ExternalDebugChecker = null;
        internal string DebugOutput = null;

        internal static readonly UInt16 SectorSize = 0x200;
        static readonly UInt16 MaxBufferBlocks = 0x7F;
        static readonly UInt16 MaxBufferSize = (UInt16)(MaxBufferBlocks * SectorSize);
        internal readonly BinRXD collection;
        readonly ReadLogic logic;

        private readonly Stream rxStream;
        private readonly PinObj rxBlock;
        private bool disposedValue;

        private delegate void ParseBufferLogic(IntPtr source);
        ParseBufferLogic ParseBuffer = null;

        public UInt64 SectorsParsed = 0;
        internal RecordCollection MessageCollection = null;
        public virtual RecordCollection Messages { get => MessageCollection; set => MessageCollection = value; }
        internal List<IFrameCollector> FrameCollectorList = [
            new J1939FrameCollector(),
            new ModeFrameCollector(),
            ];
        internal Int64 TimeOffset;
        internal BlockType LastReadRecBinType = BlockType.Unknown;
        internal byte[] LastReadRecTimestamp;

        internal protected UInt32 DataSectorStart = 0;
        protected UInt64 SectorID = 0;
        protected List<(UInt32, UInt32)> SectorMap = null;
        protected int SectorMapID = -1;

        private delegate int ReadSectorLogic(byte[] array, int offset, int count);
        private ReadSectorLogic ReadSector = null;

        public RXDataReader(BinRXD bcollection) : this(bcollection, ReadLogic.ReadData) { }

        public RXDataReader(BinRXD bcollection, ReadLogic logic = ReadLogic.ReadData)
        {
            this.logic = logic;
            collection = bcollection;
            LastReadRecBinType = BlockType.Unknown;
            LastReadRecTimestamp = new byte[4];

            ParseBuffer = GetParseLogic();
            rxStream = bcollection.GetRWStream;
            rxBlock = new PinObj(new byte[MaxBufferSize]);
            rxStream.Seek((Int64)collection.DataOffset, SeekOrigin.Begin);
            SectorID = DataSectorStart = (UInt32)(collection.DataOffset / SectorSize);

            CreateDebugFiles = collection.dataSource == DataOrigin.File && ExternalDebugChecker is not null && ExternalDebugChecker();
            DebugOutput = collection.rxdUri;

            if (CreateDebugFiles)
            {
                File.Delete(Path.ChangeExtension(DebugOutput, dbgRecordsSuffix));
                File.Delete(Path.ChangeExtension(DebugOutput, dbgSectorsSuffix));
                File.Delete(Path.ChangeExtension(DebugOutput, dbgAwsSuffix));
            }

            OutputDebugSectors();
            ReadSector = rxStream.Read;

            CheckForPreBuffers();
        }

        #region Destructors
        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    rxStream.Dispose();
                    rxBlock.Dispose();
                }

                // TODO: free unmanaged resources (unmanaged objects) and override finalizer
                // TODO: set large fields to null
                disposedValue = true;
            }
        }

        // // TODO: override finalizer only if 'Dispose(bool disposing)' has code to free unmanaged resources
        // ~RXDataReader()
        // {
        //     // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        //     Dispose(disposing: false);
        // }

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
        #endregion

        void OutputDebugSectors()
        {
            if (!CreateDebugFiles)
                return;

            string dbgSectorFileName = Path.ChangeExtension(DebugOutput, dbgSectorsSuffix);

            UInt64 sid = DataSectorStart;
            byte[] buffer = new byte[SectorSize];
            RecPreBuffer.DataRecord LastPB = new RecPreBuffer.DataRecord();
            UInt32[] PBTimestamps = new UInt32[6];
            UInt32 LastTimestamp = 0;
            UInt32 pboffset = 0;
            byte[] tmp;
            using (FileStream dbg = new FileStream(dbgSectorFileName, FileMode.Create, FileAccess.Write, FileShare.ReadWrite))
            using (FileStream fs = new FileStream(collection.rxdUri, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                fs.Seek(DataSectorStart * SectorSize, SeekOrigin.Begin);
                while (fs.Read(buffer, 0, SectorSize) > 0)
                {
                    string msg;
                    if (collection.TryGetValue(BitConverter.ToUInt16(buffer, 2), out BinBase bb) && bb.RecType == RecordType.PreBuffer)
                    {
                        bool isFirst = pboffset == 0;

                        if (isFirst)
                            pboffset = BitConverter.ToUInt32(buffer, 10) - 1;
                        else
                        {
                            msg = $"Previous PreBuffer info - PreBuffer diff: {PBTimestamps[3] - PBTimestamps[2]}; PostBuffer diff: {PBTimestamps[5] - PBTimestamps[4]}";
                            tmp = Encoding.ASCII.GetBytes(msg + Environment.NewLine);
                            dbg.Write(tmp, 0, tmp.Length);
                        }

                        LastPB.Timestamp = BitConverter.ToUInt32(buffer, 6);
                        LastPB.PreStartSector = BitConverter.ToUInt32(buffer, 10) - pboffset + DataSectorStart;
                        LastPB.PreCurrentSector = BitConverter.ToUInt32(buffer, 14) - pboffset + DataSectorStart;
                        LastPB.PreEndSector = BitConverter.ToUInt32(buffer, 18) - pboffset + DataSectorStart;
                        LastPB.PostStartSector = BitConverter.ToUInt32(buffer, 22) - pboffset + DataSectorStart;
                        LastPB.PostEndSector = BitConverter.ToUInt32(buffer, 26) - pboffset + DataSectorStart;
                        LastPB.NextPreBufferSector = BitConverter.ToUInt32(buffer, 30) - pboffset + DataSectorStart;

                        msg = $"Sector {sid:D6} (0x{sid:X4}) - PreBuffer info; " +
                            $"Timestamp: {LastPB.Timestamp}; " +
                            $"PreStart: {LastPB.PreStartSector}; " +
                            $"PreCurr: {LastPB.PreCurrentSector}; " +
                            $"PreEnd: {LastPB.PreEndSector}; " +
                            $"PostStart: {LastPB.PostStartSector}; " +
                            $"PostEnd: {LastPB.PostEndSector}; " +
                            $"NextPB: {LastPB.NextPreBufferSector}; "
                            ;
                    }
                    else
                    {
                        UInt32 ts = BitConverter.ToUInt32(buffer, 6);
                        string lh = ts < LastTimestamp ? "Lowest" : "Highest";
                        msg = $"Sector {sid:D6} (0x{sid:X4}) - Timestamp: {ts}; {lh}";
                        LastTimestamp = ts;

                        if (sid == LastPB.PreStartSector)
                            PBTimestamps[0] = ts;
                        else if (sid == LastPB.PreCurrentSector)
                            PBTimestamps[1] = ts;
                        else if (sid == LastPB.PreCurrentSector + 1)
                            PBTimestamps[2] = ts;
                        else if (sid == LastPB.PreEndSector)
                            PBTimestamps[3] = ts;
                        else if (sid == LastPB.PostStartSector)
                            PBTimestamps[4] = ts;
                        else if (sid == LastPB.PostEndSector)
                            PBTimestamps[5] = ts;
                    }
                    tmp = Encoding.ASCII.GetBytes(msg + Environment.NewLine);
                    dbg.Write(tmp, 0, tmp.Length);

                    sid++;
                }
            }
        }

        void FixGnssTimestamp(RecRaw rec)
        {
            if (rec.LinkedBin.BinType == BlockType.GNSSMessage)
            {
                if (LastReadRecBinType == BlockType.GNSSMessage)
                    Array.Copy(LastReadRecTimestamp, 0, rec.Data, 0, 4);
                else
                    Array.Copy(rec.Data, 0, LastReadRecTimestamp, 0, 4);
            }

            LastReadRecBinType = rec.LinkedBin.BinType;
        }

        ParseBufferLogic GetParseLogic()
        {
            switch (logic)
            {
                case ReadLogic.ReadData: return ParseBufferData;
                case ReadLogic.UpdateLowestTimestamp: return ParseBufferTimestamps;
                case ReadLogic.OffsetTimestamps: return OffsetTimestamps;
                case ReadLogic.ReadPreBuffers: return ParsePreBuffers;
                default: return null;
            }
        }

        void GetBlockBounds(ref IntPtr source, out IntPtr eobPtr)
        {
            UInt16 blocksize = (UInt16)Marshal.ReadInt16(source);
            source += Marshal.SizeOf(blocksize);
            if (blocksize > SectorSize - Marshal.SizeOf(blocksize)) // prevent invalid block parsing
                eobPtr = source;
            else
                eobPtr = source + blocksize;
        }

        void ParseBufferData(IntPtr source)
        {
            StringBuilder sb = null;
            StringBuilder dbg = null;
            if (CreateDebugFiles)
            {
                sb = new StringBuilder();
                sb.AppendLine("New block");
                dbg = new StringBuilder();
            }

            GetBlockBounds(ref source, out IntPtr endptr);
            List<RecBase> temprecs = new List<RecBase>();
            bool blockerror = false;
            while ((long)source < (long)endptr)
            {
                //RecRaw rec = RecRaw.Read(ref source);
                var records = RecRaw.Read(ref source);
                foreach (var rec in records)
                {
                    if (CreateDebugFiles)
                    {
                        if (rec.header.UID == 0xFFFF)
                            dbg.Append(Encoding.ASCII.GetString(rec.VariableData));
                        else
                            sb.Append(rec.ToString());
                    }

                    if (rec.header.InfSize < 4)
                    {
                        blockerror = true;
                        break;
                    }

                    if (!collection.TryGetValue(rec.header.UID, out rec.LinkedBin))
                        continue;

                    if (rec.LinkedBin.RecType == RecordType.Unknown)
                        continue;

                    FixGnssTimestamp(rec);

                    rec.BusChannel = collection.DetectBusChannel(rec.header.UID);
                    RecBase input = RecBase.Parse(rec);

                    if (input is null)
                    {
                        blockerror = true;
                        break;
                    }

                    temprecs.Add(input);
                }
                if (blockerror)
                    break;
            }

            if (!blockerror)
            {
                MessageCollection.AddRange(temprecs);
                CheckForMultiFrame();
            }

            if (CreateDebugFiles)
            {
                if (sb.Length > 0)
                    using (var reclog = File.AppendText(Path.ChangeExtension(DebugOutput, dbgRecordsSuffix)))
                        reclog.Write(sb.ToString());
                if (dbg.Length > 0)
                    using (var reclog = File.AppendText(Path.ChangeExtension(DebugOutput, dbgAwsSuffix)))
                        reclog.Write(dbg.ToString());
            }
        }

        void ParseBufferTimestamps(IntPtr source)
        {
            GetBlockBounds(ref source, out IntPtr endptr);
            while ((long)source < (long)endptr)
            {
                //RecRaw rec = RecRaw.Read(ref source);
                var records = RecRaw.Read(ref source);
                foreach (var rec in records)
                    if (collection.TryGetValue(rec.header.UID, out rec.LinkedBin))
                    {
                        if (rec.LinkedBin.BinType == BlockType.Trigger)
                            if (rec.header.InfSize != 4 || rec.header.DLC != 1)
                                continue;

                        FixGnssTimestamp(rec);

                        var timestamp = rec.ExtractRawTimestamp;
                        if (rec.LinkedBin.DataFound && timestamp < rec.LinkedBin.LastTimestamp)
                            rec.LinkedBin.TimeOverlap = true;

                        rec.LinkedBin.LastTimestamp = timestamp;

                        if (!rec.LinkedBin.DataFound)
                        {
                            rec.LinkedBin.FirstTimestamp = rec.LinkedBin.LastTimestamp;
                            rec.LinkedBin.DataFound = true;
                        }
                    }
            }
        }

        void OffsetTimestamps(IntPtr source)
        {
            if (TimeOffset == 0)
                return;

            GetBlockBounds(ref source, out IntPtr endptr);
            while ((long)source < (long)endptr)
                RecRaw.ApplyTimestampOffset(ref source, TimeOffset);
        }

        public void ReadLiveBuffer(PinObj buffer, int BlockCount)
        {
            MessageCollection = new RecordCollection();
            for (int i = 0; i < BlockCount; i++)
                ParseBufferData((IntPtr)buffer + i * 512);
        }

        void ParsePreBuffers(IntPtr source)
        {
            GetBlockBounds(ref source, out IntPtr endptr);
            while ((long)source < (long)endptr)
            {
                //RecRaw rec = RecRaw.Read(ref source);
                var records = RecRaw.Read(ref source);
                foreach (var rec in records)
                {
                    if (!collection.TryGetValue(rec.header.UID, out rec.LinkedBin))
                        continue;

                    if (rec.LinkedBin.RecType != RecordType.PreBuffer)
                        continue;

                    FixGnssTimestamp(rec);

                    //rec.BusChannel = collection.DetectBusChannel(rec.header.UID);
                    RecBase input = RecBase.Parse(rec);

                    if (input is null)
                        break;

                    MessageCollection.Add(input);
                }
            }
        }

        int ReadPreBufferSector(byte[] array, int offset, int count)
        {
            //if (SectorMap[SectorMapID].Item2 == 0)
            //return 0;
            if (SectorID == SectorMap[SectorMapID].Item1)
            {
                rxStream.Seek(SectorMap[SectorMapID].Item2 * SectorSize, SeekOrigin.Begin);
                SectorMapID++;
            }
            SectorID++;
            return rxStream.Read(array, offset, count);
        }

        public virtual bool ReadNext()
        {
            MessageCollection = null;
            try
            {
                // Data block
                if (ReadSector(rxBlock, 0, SectorSize) != SectorSize)
                    return false;

                UInt16 blocksize = (UInt16)Marshal.ReadInt16(rxBlock);
                int blocks = ((2 + blocksize + 0x1ff) & ~(UInt16)0x1ff) / SectorSize;
                if (blocks > 1)
                    ReadSector(rxBlock, (blocks - 1) * SectorSize, SectorSize);

                MessageCollection = new RecordCollection();
                MessageCollection.ID = ++SectorsParsed;
                ParseBuffer?.Invoke(rxBlock);
                if (logic == ReadLogic.OffsetTimestamps && TimeOffset != 0)
                {
                    rxStream.Seek(-SectorSize, SeekOrigin.Current);
                    rxStream.Write(rxBlock, 0, SectorSize);
                }

                return true;
            }
            catch (Exception e)
            {
                return false;
            }
        }

        public UInt32 ReadSectorHighestTimestamp(Int64 fileposTimeOffset)
        {
            try
            {
                rxStream.Seek(fileposTimeOffset, SeekOrigin.Begin);
                if (rxStream.Read(rxBlock, 0, SectorSize) != SectorSize)
                    return 0;

                UInt16 blocksize = (UInt16)Marshal.ReadInt16(rxBlock);
                int blocks = ((2 + blocksize + 0x1ff) & ~(UInt16)0x1ff) / SectorSize;
                if (blocks > 1)
                    ReadSector(rxBlock, SectorSize, (blocks - 1) * SectorSize);

                UInt32 LastTime = 0;
                IntPtr source = rxBlock;
                GetBlockBounds(ref source, out IntPtr endptr);
                while ((long)source < (long)endptr)
                {
                    //RecRaw rec = RecRaw.Read(ref source);
                    var records = RecRaw.Read(ref source);
                    foreach (var rec in records)
                    {
                        if (!collection.TryGetValue(rec.header.UID, out rec.LinkedBin))
                            continue;

                        FixGnssTimestamp(rec);

                        LastTime = Math.Max(LastTime, BitConverter.ToUInt32(rec.Data, 0));
                    }
                }

                return LastTime;
            }
            catch (Exception)
            {
                return 0;
            }
        }

        public Int64 GetProgress => (rxStream.Position * 100) / collection.rxdSize;

        void CheckForMultiFrame()
        {
            for (int i = 0; i < MessageCollection.Count; i++)
                foreach (var collector in FrameCollectorList)
                    if (collector.TryCollect(MessageCollection[i], this))
                        break;
        }

        bool isActive()
        {
            try
            {
                byte[] tmp = new byte[SectorSize];
                rxStream.Seek(-SectorSize, SeekOrigin.End);
                rxStream.Read(tmp, 0, SectorSize);
                return tmp.All(b => b == 0);
            }
            catch
            {
                return false;
            }
        }

        internal UInt32 GetFilePreBufferInitialTimestamp
        {
            get
            {
                if (collection.PreBuffers.Count > 0)
                    if (collection.PreBuffers[0].data.PreStartSector == DataSectorStart)
                        return collection.PreBuffers[0].data.InitialTimestamp;

                return 0;
            }
        }

        void CheckForPreBuffers()
        {
            if (collection.PreBuffers is null)
            {
                collection.PreBuffers = new PreBufferCollection();

                var oldLogic = ParseBuffer;
                try
                {
                    ParseBuffer = ParsePreBuffers;

                    UInt32 SectorOffset = 0;
                    while (ReadNext())
                    {
                        // Probably an error
                        if (MessageCollection.Count == 0)
                            continue;

                        RecPreBuffer pb = MessageCollection[0] as RecPreBuffer;
                        if (SectorOffset == 0)
                            SectorOffset = (UInt32)(pb.data.PreStartSector - (rxStream.Position / SectorSize));

                        pb.FixOffsetBy(SectorOffset);
                        collection.PreBuffers.Add(pb);
                        if (pb.data.isLast)
                            break;

                        rxStream.Seek(pb.data.NextPreBufferSector * SectorSize, SeekOrigin.Begin);
                    }
                }
                catch { }
                finally
                {
                    collection.PreBuffers.IncludeLastUntriggered = !isActive();
                    ParseBuffer = oldLogic;
                    rxStream.Seek((Int64)collection.DataOffset, SeekOrigin.Begin);
                }
            }

            if (collection.PreBuffers is not null)
            {
                SectorMap = collection.PreBuffers.GetSectorMap();
                if (SectorMap.Count > 0)
                {
                    SectorMap.Add((0, 0));
                    SectorMapID = 0;
                    ReadSector = ReadPreBufferSector;
                }
            }
        }

    }
}
