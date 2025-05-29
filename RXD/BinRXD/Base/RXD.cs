using InfluxShared.FileObjects;
using InfluxShared.Generic;
using InfluxShared.Helpers;
using InfluxShared.Objects;
using MDF4xx.Blocks;
using MDF4xx.Frames;
using MDF4xx.IO;
using RXD.Blocks;
using RXD.DataRecords;
using RXD.Helpers;
using RXD.Objects;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using System.Xml.Schema;

namespace RXD.Base
{
    public enum DataOrigin : byte { File, Memory }

    public class BinRXD : BlockCollection, IDisposable
    {
        public class ExportSettings
        {
            public StorageCacheType StorageCache = StorageCacheType.Disk;
            public List<UInt16> ChannelFilter = null;
            public ExportCollections SignalsDatabase = null;
            public ProcessingRulesCollection ProcessingRules = null;
            public Action<object> ProgressCallback = null;
        }

        private const string DateTimeFormat = "yyyyMMdd_HHmmss";

        public static readonly string Extension = ".rxd";
        public static readonly string EncryptedExtension = ".rxe";
        public static readonly string BinExtension = ".rxc";
        public static readonly string Filter = "ReX data (*.rxd)|*.rxd";
        public static readonly string EncryptedFilter = "ReX encrypted data (*.rxe)|*.rxe";
        public static readonly string BinFilter = "ReX configuration (*.rxc)|*.rxc";
        public static readonly string SortedSuffix = "_sorted";
        public static readonly bool AllowSorting = false;
        public static bool DoSort = AllowSorting;
        public static TCModeUnits TCMode = TCModeUnits.Disabled;

        public static string EncryptionContainerName = "ReXgen";
        public static byte[] EncryptionKeysBlob = null;

        public static UInt64 MaxTimeGap = 0; //100 * 1000 * 100; // Config[BinConfig.BinProp.TimeStampPrecision]
        public static bool InternalTimestamp = false;
        public static int InternalTimestampSize = 2;
        public UInt64 TimeRoll = 0x100000000;

        static byte headersizebytes = 4;
        internal static UInt32 StructureOffset = 0;
        internal readonly DataOrigin dataSource;
        readonly internal string rxdUri = "";
        readonly internal string rxeUri = "";
        internal string reloadSorted = string.Empty;
        readonly internal byte[] rxdBytes = null;
        public string Error = "";

        Int64 rxdFullSize;
        public Int64 rxdSize => rxdFullSize;
        public readonly DateTime DatalogStartTime;
        public readonly string SerialNumber;
        public string DatalogStartTimeAsString => DatalogStartTime.ToString(DateTimeFormat);

        public bool Empty => Count == 0;

        internal UInt64 DataOffset = 0;

        public RXDLoggerCollection AttachedLoggers = null;
        private bool disposedValue;

        private BinRXD(string uri = "", Stream dataStream = null, Stream xsdStream = null)
        {
            TimeRoll = InternalTimestamp ? (ulong)(1 << (InternalTimestampSize * 8)) : 0x100000000;
            rxdFullSize = 0;
            Error = "";

            if (uri != String.Empty && new Uri(uri).IsFile)
                dataSource = DataOrigin.File;
            else if (dataStream != null)
                dataSource = DataOrigin.Memory;
            else
                return;

            // If no file data provided then create empty object
            if (uri is null || uri == string.Empty)
                return;

            // If file not exist then throw an exception
            else if (dataSource == DataOrigin.File && !File.Exists(uri))
                throw new Exception("File does not exist!");

            // If file is XML then read XML structure
            else if (Path.GetExtension(uri).Equals(XmlHandler.Extension, StringComparison.OrdinalIgnoreCase))
                switch (dataSource)
                {
                    case DataOrigin.File: ReadXMLStructure(uri); break;
                    case DataOrigin.Memory: ReadXMLStructure(dataStream, xsdStream); break;
                }

            else
            {
                // If file is encrypted
                if (Path.GetExtension(uri).Equals(EncryptedExtension, StringComparison.OrdinalIgnoreCase))
                {
                    switch (dataSource)
                    {
                        case DataOrigin.File:
                            rxeUri = uri;
                            rxdUri = Path.Combine(PathHelper.TempPath, Path.ChangeExtension(Path.GetFileName(uri), Extension));
                            if (!RXEncryption.DecryptFile(rxeUri, rxdUri))
                                throw new Exception("Access to encrypted data is rejected!");
                            goto Processing;
                        case DataOrigin.Memory:
                            throw new Exception("Encryption stream data is not supported!");
                    }
                }

                // Make local copy if needed
                if (dataSource == DataOrigin.File && !PathHelper.hasWriteAccessToFile(uri))
                {
                    string newpath = Path.Combine(PathHelper.TempPath, Path.GetFileName(uri));
                    File.Copy(uri, newpath, true);
                    uri = newpath;
                }
                if (dataSource == DataOrigin.Memory)
                {
                    using (MemoryStream ms = new())
                    {
                        dataStream.CopyTo(ms);
                        rxdBytes = ms.ToArray();
                    }
                }
                rxdUri = uri;

            Processing:

                DatalogStartTime = FileNameToDateTime(Path.GetFileNameWithoutExtension(rxdUri));
                SerialNumber = FileNameToSerialNumber(Path.GetFileNameWithoutExtension(rxdUri));
                if (!ReadRXD())
                    throw new Exception("Error reading RXC data!");

                if (Config is null || Count == 0)
                    throw new Exception("Not a valid RXD file!");

            }
        }

        public static BinRXD Create() => new BinRXD();

        public static BinRXD Load(string path = null)
        {
            try
            {
                var rxd = new BinRXD(path);
                if (rxd != null)
                    if (rxd.reloadSorted != string.Empty)
                    {
                        BinRXD.DoSort = false;
                        return new BinRXD(rxd.reloadSorted);
                    }

                return rxd;
            }
            catch (Exception e)
            {
                return null;
            }
        }

        /// <summary>
        /// Example usage:
        /// string uri = "https://bucket.s3.eu-central-1.amazonaws.com/datalogs/RexGen Air config 500kb_0001902_20221006_103901.rxd";
        /// FileStream fs = new FileStream(fn, FileMode.Open, FileAccess.Read);
        /// BinRXD r = BinRXD.Load(uri, fs);
        /// FileStream fw = new FileStream(outfn, FileMode.Create);
        /// DataHelper.Convert(r, null, fw, "csv");
        /// </summary>
        /// <param name="uri"></param>
        /// <param name="dataStream"></param>
        /// <param name="xsdStream"></param>
        /// <returns></returns>
        public static BinRXD Load(string uri, Stream dataStream, Stream xsdStream = null)
        {
            try
            {
                return new BinRXD(uri, dataStream, xsdStream);
            }
            catch (Exception e)
            {
                return null;
            }
        }

        #region Destructors
        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    if (Path.GetExtension(rxdUri).Equals(EncryptedExtension, StringComparison.OrdinalIgnoreCase))
                        File.Delete(rxdUri);
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

        private DateTime FileNameToDateTime(string fn)
        {
            const string pattern = "\\d{8}_\\d{6}";
            DateTime dt;

            foreach (var dtstr in Regex.Matches(fn, pattern, RegexOptions.RightToLeft ).Cast<Match>().Where(m => m.Success))
                if (DateTime.TryParseExact(dtstr.Value, DateTimeFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out dt))
                    return dt;

            return DateTime.Now;
        }

        private string FileNameToSerialNumber(string fn)
        {
            const string pattern = "_\\d{7}_";

            foreach (var snstr in Regex.Matches(fn, pattern).Cast<Match>().Where(m => m.Success).Reverse())
                if (int.TryParse(snstr.Value.Substring(1, 7), out _))
                    return snstr.Value.Substring(1, 7);

            return "0";
        }

        Stream GetStream => dataSource switch
        {
            DataOrigin.File => new FileStream(rxdUri, FileMode.Open, FileAccess.Read, FileShare.ReadWrite),
            DataOrigin.Memory => new MemoryStream(rxdBytes),
            _ => null,
        };

        internal Stream GetRWStream => dataSource switch
        {
            DataOrigin.File => new FileStream(rxdUri, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite),
            DataOrigin.Memory => new MemoryStream(rxdBytes),
            _ => null,
        };

        public bool ToRXData(Stream rxStream)
        {
            try
            {
                GetStream.CopyTo(rxStream);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public bool ReadRXC(Stream rxdStream)
        {
            try
            {
                using (BinaryReader br = new BinaryReader(rxdStream))
                {
                    Config = (BinConfig)BinBase.ReadNext(br);
                    if (Config == null)
                        return false;

                    if (InternalTimestamp)
                        Config[BinConfig.BinProp.TimeStampPrecision] = 1;
                    TimestampCoeff = Config[BinConfig.BinProp.TimeStampPrecision] * 0.000001;
                    if (Config[BinConfig.BinProp.GUID] == Guid.Empty)
                        return false;

                    while (br.BaseStream.Position != br.BaseStream.Length)
                    {
                        BinBase binblock = BinBase.ReadNext(br);
                        if (binblock == null)
                            break;
                        Add(binblock);
                    }
                }

                return true;
            }
            catch
            {
                return false;
            }
        }

        bool ReadRXD()
        {
            try
            {
                using (var rxStream = GetStream)
                {
                    rxdFullSize = rxStream.Seek(0, SeekOrigin.End);
                    rxStream.Seek(StructureOffset, SeekOrigin.Begin);

                    UInt32 hdrSize = (UInt32)rxdFullSize;
                    if (Path.GetExtension(rxdUri).Equals(Path.GetExtension(Extension), StringComparison.OrdinalIgnoreCase))
                    {
                        byte[] hdrsize = new byte[headersizebytes];
                        rxStream.Read(hdrsize, 0, headersizebytes);
                        hdrSize = BitConverter.ToUInt32(hdrsize, 0);
                    }

                    if (!ReadRXC(rxStream))
                        return false;

                    DataOffset = (UInt64)(headersizebytes + hdrSize);
                    DataOffset = (DataOffset + 0x1ff) & ~(UInt32)0x1ff;
                }

                DetectFirstTimestamp();
                if (SortFile(out string sortfn))
                    reloadSorted = sortfn;
                BinRXD.DoSort = AllowSorting;

                return true;
            }
            catch
            {
                return false;
            }
        }

        public bool ToRXD(Stream rxdStream, bool StructOnly = true)
        {
            try
            {
                using (MemoryStream ms = new MemoryStream())
                {
                    var cfgblocks = new BinBase[] { Config, ConfigFTP, ConfigMobile, ConfigS3, ConfigWiFi };
                    foreach (var cfgbin in cfgblocks)
                        if (cfgbin is not null)
                        {
                            byte[] data = cfgbin.ToBytes();
                            ms.Write(data, 0, data.Length);
                        }

                    foreach (var bin in this)
                    {
                        if (bin.Value is BinCanMessage)
                        {
                            var msg = bin.Value as BinCanMessage;
                            byte[] hex = msg[BinCanMessage.BinProp.DefaultHex];
                            if (msg[BinCanMessage.BinProp.DLC] != hex.Length)
                            {
                                msg[BinCanMessage.BinProp.DefaultHex] = new byte[msg[BinCanMessage.BinProp.DLC]];
                                for (int i = 0; i < msg[BinCanMessage.BinProp.DLC]; i++)
                                {
                                    if (i < Math.Min(hex.Length, msg[BinCanMessage.BinProp.DLC]))
                                        msg[BinCanMessage.BinProp.DefaultHex][i] = hex[i];
                                }
                            }
                        }// Fix if the default bytes are not equal to the dlc. Causes Rexgen to return error

                        byte[] data = bin.Value.ToBytes();
                        ms.Write(data, 0, data.Length);
                    }

                    using (BinaryWriter bw = new BinaryWriter(rxdStream, Encoding.ASCII, true))
                    {
                        if (!StructOnly)
                            bw.Write((UInt32)ms.Length);
                        bw.Write(ms.ToArray());
                        bw.Flush();
                        if (!StructOnly)
                            while (rxdStream.Position % RXDataReader.SectorSize != 0)
                                bw.Write((byte)0);
                        bw.Flush();
                        rxdStream.Position = 0;
                    }
                }

                return true;
            }
            catch
            {
                return false;
            }
        }

        public bool ToRXD(string rxdFileName, bool StructOnly = true)
        {
            try
            {
                using (FileStream fw = new FileStream(rxdFileName, FileMode.Create))
                    return ToRXD(fw, StructOnly);
            }
            catch
            {
                return false;
            }
        }

        public bool SortFile(out string sortfn, Action<object> ProgressCallback = null)
        {
            sortfn = string.Empty;
            if (!BinRXD.DoSort)
                return false;

            try
            {
                if (dataSource == DataOrigin.Memory)
                    throw new Exception("Sortng stream data is not supported!");

                if (reloadSorted == rxdUri)
                    return false;

                if (rxdUri.EndsWith(SortedSuffix + Extension))
                    return false;

                string sortedfn = rxdUri.Replace(Extension, SortedSuffix + Extension);
                if (File.Exists(sortedfn))
                {
                    sortfn = sortedfn;
                    return true;
                }

                if (!ToRXD(sortedfn, false))
                    throw new Exception("Writing structure failed!");

                byte[] buffer = new byte[RXDataReader.SectorSize];
                UInt16 buffsize = 0;
                Dictionary<RecordType, Queue<RecBase>> data = new Dictionary<RecordType, Queue<RecBase>>();
                UInt64 SectorsRead = 0;

                bool FindLowestTime(out UInt32 time)
                {
                    time = UInt32.MaxValue;
                    bool found = false;
                    foreach (var channel in data.Values)
                        if (channel.Count > 0)
                        {
                            time = Math.Min(time, channel.Peek().RawTimestamp);
                            found = true;
                        }
                    return found;
                }

                void WriteBlock(FileStream fs)
                {
                    buffer[0] = (byte)(buffsize & 0xff);
                    buffer[1] = (byte)((buffsize >> 8) & 0xff);
                    fs.Write(buffer, 0, buffer.Length);
                    Array.Clear(buffer, 0, buffer.Length);
                    buffsize = 0;
                    SectorsRead--;
                }

                UInt32 timestamp;
                using (RXDataReader dr = new(this))
                using (FileStream fs = new FileStream(sortedfn, FileMode.Open, FileAccess.ReadWrite))
                {
                    void AddToBlock(byte[] rb)
                    {
                        if (buffsize + rb.Length > RXDataReader.SectorSize - 2)
                            WriteBlock(fs);
                        Buffer.BlockCopy(rb, 0, buffer, 2 + buffsize, rb.Length);
                        buffsize += (UInt16)rb.Length;
                    }

                    void AddMessages()
                    {
                        foreach (var q in data.Values)
                            if (q.Count > 0)
                                while (q.Peek().RawTimestamp == timestamp)
                                {
                                    AddToBlock(q.Dequeue().ToBytes());
                                    if (q.Count() == 0)
                                        break;
                                }
                    }

                    fs.Seek(0, SeekOrigin.End);
                    while (dr.ReadNext())
                    {
                        SectorsRead++;
                        foreach (RecBase rec in dr.Messages)
                            if (rec.LinkedBin.RecType != RecordType.PreBuffer)
                            {
                                if (!data.ContainsKey(rec.LinkedBin.RecType))
                                    data[rec.LinkedBin.RecType] = new Queue<RecBase>();

                                data[rec.LinkedBin.RecType].Enqueue(rec);
                            }
                        ProgressCallback?.Invoke((int)dr.GetProgress);
                        while (SectorsRead > 100)
                        {
                            if (FindLowestTime(out timestamp))
                                AddMessages();
                        }
                    }
                    while (FindLowestTime(out timestamp))
                        AddMessages();
                }

                sortfn = sortedfn;
                return true;
            }
            catch (Exception ex)
            {
                sortfn = string.Empty;
                return false;
            }
        }

        public bool ToMF4(string outputfn, ExportSettings settings = null, Action<object> ProgressCallback = null)
        {
            using (FileStream fs = new FileStream(outputfn, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                return ToMf4Stream(fs, settings, ProgressCallback);
            }
        }

        public Stream ToMF4(ExportSettings settings = null, Action<object> ProgressCallback = null)
        {
            MemoryStream ms = new MemoryStream();
            ToMf4Stream(ms, settings, ProgressCallback);
            return ms;
        }

        bool ToMf4Stream(Stream mdfStream, ExportSettings settings = null, Action<object> ProgressCallback = null)
        {
            object FindMessageFrameID(RecBase rec, out UInt16 GroupID, out byte DLC)
            {
                if (rec is RecCanTrace)
                {
                    foreach (var fmsg in settings.SignalsDatabase.dbcCollection)
                        if (DbcEqualsRecord(fmsg, rec as RecCanTrace))
                        {
                            DLC = fmsg.Message.DLC;
                            GroupID = (UInt16)fmsg.uniqueid;
                            return fmsg;
                        }
                }
                else if (rec is RecLinTrace)
                {
                    if (((rec as RecLinTrace).data.Flags & LinMessageFlags.Error) == 0)
                        foreach (var fmsg in settings.SignalsDatabase.ldfCollection)
                            if (LdfEqualsRecord(fmsg, rec as RecLinTrace))
                            {
                                DLC = fmsg.Message.DLC;
                                GroupID = (UInt16)fmsg.uniqueid;
                                return fmsg;
                            }
                }

                GroupID = 0;
                DLC = 0;
                return null;
            }

            settings ??= new();
            bool Exportable(BinBase bin) => settings.ChannelFilter is null || settings.ChannelFilter.Contains(bin.header.uniqueid);

            UInt64 LastTimestampCan = 0;
            UInt64 TimeOffsetCan = 0;
            UInt64 LastTimestampCanError = 0;
            UInt64 TimeOffsetCanError = 0;
            UInt64 LastTimestampLin = 0;
            UInt64 TimeOffsetLin = 0;

            ProgressCallback?.Invoke(0);
            ProgressCallback?.Invoke("Writing MF4 file...");
            try
            {
                Dictionary<UInt16, ChannelDescriptor> Signals =
                     this.Where(r => r.Value.RecType == RecordType.MessageData && Exportable(r.Value)).
                     Select(dg => new { ID = (UInt16)dg.Key, Data = dg.Value.GetDataDescriptor }).
                     ToDictionary(dg => dg.ID, dg => dg.Data);

                MDF mdf = new MDF();
                using (BlockBuilder builder = new BlockBuilder(mdf, 8/*Config[BinConfig.BinProp.TimeStampSize]*/, Config[BinConfig.BinProp.TimeStampPrecision]))
                {
                    mdf.BuildLoggerStruct(builder, DatalogStartTime, Signals, settings.SignalsDatabase);
                    mdf.Write(mdfStream);

                    var mdfGroups =
                        mdf.Values.OfType<DGBlock>().
                        ToDictionary(
                            g => (FrameType)(g.links.GetObject(DGLinks.dg_cg_first) as CGBlock).data.cg_record_id,
                            g => new
                            {
                                cgblock = (CGBlock)g.links.GetObject(DGLinks.dg_cg_first),
                                dgblock = g,
                                dlblock = g.GetDLBlock(),
                            }
                        );

                    foreach (var group in mdfGroups)
                    {
                        group.Value.dlblock.CreateNewData(builder).CreateWriteBuffers();
                        /*if (group.Value.dlblock.CurrentDataBlock is DZBlock dz)
                            dz.data.dz_zip_parameter = group.Value.cgblock.data.cg_size.cg_data_bytes + group.Value.dgblock.data.dg_rec_id_size;*/
                    }

                    Type readerType = AttachedLoggers is null ? typeof(RXDataReader) : typeof(RXDataSyncReader);
                    using (var dr = (RXDataReader)Activator.CreateInstance(readerType, this))
                    {
                        UInt32 InitialTimestamp = dr.GetFilePreBufferInitialTimestamp;
                        bool FirstTimestampRead = false;
                        UInt32 FileTimestamp = 0;

                        void FinishCurrentDLdatablock(DLBlock dl)
                        {
                            dl.CurrentDataBlock.EndWriting();
                            BaseBlock db = (BaseBlock)dl.CurrentDataBlock;
                            if (dl.CurrentDataBlock.OrigDatalength > 0)
                            {
                                db.extraObj = dl.CurrentDataBlock.GetStream.ToArray();
                                db.SetWriteFileLink(ref builder.lastlink);
                                dl.AppendCurrentDataBlock();

                                var tempArr = db.ToBytes();
                                mdfStream.Seek(db.flink, SeekOrigin.Begin);
                                mdfStream.Write(tempArr, 0, tempArr.Length);
                            }

                            dl.CurrentDataBlock.FreeWriteBuffers();
                        }

                        void WriteMdfFrame(BaseDataFrame frame, RecordType frameType)
                        {
                            if (frame == null)
                                return;

                            if (mdfGroups.ContainsKey(frame.data.Type))
                                mdfGroups[frame.data.Type].cgblock.data.cg_cycle_count++;

                            if (!FirstTimestampRead)
                            {
                                FirstTimestampRead = true;
                                FileTimestamp = (uint)(InitialTimestamp == 0 ? FirstTimestamp : Math.Min(InitialTimestamp, frame.data.Timestamp));
                            }

                            void CheckTimeOverlap(ref UInt64 LastTimestamp, ref UInt64 TimeOffset)
                            {
                                if (frame.data.Timestamp < LastTimestamp)
                                    TimeOffset += TimeRoll;
                                LastTimestamp = frame.data.Timestamp;
                                frame.data.Timestamp += TimeOffset - FileTimestamp;
                            }

                            switch (frameType)
                            {
                                case RecordType.CanTrace:
                                    CheckTimeOverlap(ref LastTimestampCan, ref TimeOffsetCan);
                                    break;
                                case RecordType.CanError:
                                    CheckTimeOverlap(ref LastTimestampCanError, ref TimeOffsetCanError);
                                    break;
                                case RecordType.LinTrace:
                                    CheckTimeOverlap(ref LastTimestampLin, ref TimeOffsetLin);
                                    break;
                                default:
                                    CheckTimeOverlap(ref LastTimestampCan, ref TimeOffsetCan);
                                    break;
                            }

                            if (mdfGroups.ContainsKey(frame.data.Type))
                            {
                                DLBlock dl = mdfGroups[frame.data.Type].dlblock;
                                dl.CurrentDataBlock.WriteFrame(frame);
                                if (dl.CurrentDataBlock.OrigDatalength > MDF.DefaultDataBlockLength)
                                {
                                    FinishCurrentDLdatablock(dl);
                                    dl.CreateNewData(builder).CreateWriteBuffers();
                                    /*if (dl.CurrentDataBlock is DZBlock dz)
                                        dz.data.dz_zip_parameter = mdfGroups[frame.data.Type].cgblock.data.cg_size.cg_data_bytes + mdfGroups[frame.data.Type].dgblock.data.dg_rec_id_size;*/

                                }
                            }
                        }

                        object fmsg;
                        int midx;
                        while (dr.ReadNext())
                        {
                            foreach (RecBase rec in dr.Messages)
                            {
                                foreach (var mdfframe in rec.ToMdfFrame())
                                    WriteMdfFrame(mdfframe, rec.LinkedBin.RecType);

                                if (settings.SignalsDatabase != null)
                                    if ((fmsg = FindMessageFrameID(rec, out UInt16 groupid, out byte DLC)) != null)
                                    {
                                        WriteMdfFrame(rec.ConvertToMdfMessageFrame(groupid, DLC), rec.LinkedBin.RecType);
                                        if (fmsg is ExportDbcMessage msg)
                                            foreach (var mp in msg.MultiplexDict)
                                                if (mp.Value.multiplexor is not null)
                                                    if (mp.Value.multiplexorData.ExtractHex(rec.VariableData, out BinaryData.HexStruct hex))
                                                        if ((midx = mp.Value.multiplexorMap.Keys.ToList().IndexOf((UInt64)mp.Value.multiplexorData.CalcValue(ref hex))) != -1)
                                                            WriteMdfFrame(rec.ConvertToMdfMessageFrame((ushort)mp.Value.multiplexorGroups[midx], DLC), rec.LinkedBin.RecType);
                                    }
                            }
                            ProgressCallback?.Invoke((int)dr.GetProgress);
                        }

                        foreach (var group in mdfGroups)
                            FinishCurrentDLdatablock(group.Value.dlblock);
                    }

                    using (BinaryWriter bw = new BinaryWriter(mdfStream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true), true))
                        foreach (var group in mdfGroups)
                        {
                            var dl = group.Value.dlblock;
                            var dg = group.Value.dgblock;
                            var cg = group.Value.cgblock;

                            if (dl.data.dl_count == 0)
                            {
                                dl.UpdateParent(dg);
                                dg.links.SetObject(DGLinks.dg_data, null);
                            }
                            else
                            {
                                dl.SetWriteFileLink(ref builder.lastlink);
                                dl.UpdateParent(dg);

                                bw.Seek((int)dl.flink, SeekOrigin.Begin);
                                bw.Write(dl.ToBytes());

                                if (dg.dg_data is HLBlock hl)
                                {
                                    bw.Seek((int)hl.flink, SeekOrigin.Begin);
                                    bw.Write(hl.ToBytes());
                                }
                            }

                            bw.Seek((int)dg.flink, SeekOrigin.Begin);
                            bw.Write(dg.ToBytes());

                            bw.Seek((int)cg.flink, SeekOrigin.Begin);
                            bw.Write(cg.ToBytes());
                        }
                }
                ProgressCallback?.Invoke(100);
                //Console.WriteLine("file written successfully");
                return true;
            }
            catch (Exception e)
            {
                //MessageBox.Show(e.Message);
                return false;
            }
        }

        public DoubleDataCollection ToDoubleData(ExportSettings settings = null)
        {
            settings ??= new();

            bool FindMessageFrameID(RecBase rec, out int Index)
            {
                if (rec is RecCanTrace recCan)
                {
                    for (Index = 0; Index < settings.SignalsDatabase.dbcCollection.Count; Index++)
                        if (DbcEqualsRecord(settings.SignalsDatabase.dbcCollection[Index], recCan))
                            return true;
                }
                else if (rec is RecLinTrace recLin)
                {
                    if (((rec as RecLinTrace).data.Flags & LinMessageFlags.Error) == 0)
                        for (Index = 0; Index < settings.SignalsDatabase.ldfCollection.Count; Index++)
                            if (LdfEqualsRecord(settings.SignalsDatabase.ldfCollection[Index], recLin))
                                return true;
                }

                Index = -1;
                return false;
            }

            bool Exportable(BinBase bin) => settings.ChannelFilter is null || settings.ChannelFilter.Contains(bin.header.uniqueid);

            DoubleDataCollection ddata = new(SerialNumber, settings.StorageCache);
            ddata.RealTime = DatalogStartTime;
            ddata.ProcessingRules = settings.ProcessingRules;

            UInt32 FileTimestamp = 0;
            UInt64[] LastTimestamp = new UInt64[UInt16.MaxValue + 1];
            UInt64[] TimeOffset = new UInt64[UInt16.MaxValue + 1];
            for (var i = 0; i <= UInt16.MaxValue; i++)
            {
                LastTimestamp[i] = 0;
                TimeOffset[i] = (this.TryGetValue(i, out BinBase b) && b.AddOverlap) ? TimeRoll : (UInt64)0;
            }

            bool isLastBlock = false;
            bool bigGapFound = false;

            try
            {
                double WriteData(DoubleData dd, UInt64 Timestamp, byte[] BinaryArray, ref UInt64 LastTimestamp, ref UInt64 TimeOffset)
                {
                    if (Timestamp < LastTimestamp)
                        TimeOffset += TimeRoll;
                    if (LastTimestamp > 0 && MaxTimeGap > 0 && Timestamp + TimeOffset - LastTimestamp > MaxTimeGap)
                    {
                        bigGapFound = true;
                        return double.NaN;
                    }

                    LastTimestamp = Timestamp;
                    Timestamp += TimeOffset;

                    return dd.WriteBinaryData((Timestamp - FileTimestamp) * TimestampCoeff, BinaryArray);
                }

                settings.ProgressCallback?.Invoke(0);
                settings.ProgressCallback?.Invoke("Extracting channel data...");
                using (RXDataReader dr = new RXDataReader(this))
                {
                    UInt32 InitialTimestamp = dr.GetFilePreBufferInitialTimestamp;
                    FileTimestamp = InitialTimestamp == 0 ? FirstTimestamp : InitialTimestamp;
                    //ddata.FirstTimestamp = InitialTimestamp == 0 ? double.NaN : (InitialTimestamp * TimestampCoeff);
                    if (settings.ProcessingRules is not null)
                        settings.ProcessingRules.FirstTime = (FirstTimestamp - FileTimestamp) * TimestampCoeff;

                    if (settings.SignalsDatabase is not null && settings.SignalsDatabase.dbcCollection is not null)
                        foreach (var msg in settings.SignalsDatabase.dbcCollection)
                            foreach (BasicItemInfo sig in msg.Signals)
                                sig.TempObj = null;

                    while (dr.ReadNext())
                    {
                        isLastBlock = (dr.Messages.ID + dr.DataSectorStart) * RXDataReader.SectorSize == (UInt64)rxdFullSize;

                        foreach (RecBase rec in dr.Messages)
                        {
                            switch (rec.LinkedBin.RecType)
                            {
                                case RecordType.Unknown:
                                    break;
                                case RecordType.CanTrace:
                                    RecCanTrace canrec = rec as RecCanTrace;
                                    if (settings.SignalsDatabase != null)
                                        if (FindMessageFrameID(canrec, out int id))
                                        {
                                            ExportDbcMessage busMsg = settings.SignalsDatabase.dbcCollection[id];

                                            if (!UDSMap.TryGetValue(canrec.CustomType, out byte canuds))
                                                break;

                                            var Signals = busMsg.Signals.Where(s => s.UDS == canuds).ToList();
                                            if (Signals.Count > 0)
                                            {
                                                byte SA = (byte)((busMsg.Message.MsgType == DBCMessageType.J1939PG) ? canrec.data.CanID.Source : 0xFF);

                                                var mode = busMsg.GetMode(canuds);
                                                double modeval = double.NaN;
                                                double lastval = double.NaN;
                                                for (int i = 0; i < Signals.Count; i++)
                                                {
                                                    var sig = Signals[i];
                                                    var obj = ddata.Object(sig as BasicItemInfo, (1u << 30) | ((uint)busMsg.Signals.IndexOf(sig) << 16) | (uint)id, SA);
                                                    obj.BusChannel = $"CAN{canrec.BusChannel}";

                                                    if (i == 1 && mode is not null)
                                                        modeval = lastval;
                                                    if ((sig.Type == DBCSignalType.ModeDependent && sig.Mode == modeval) || sig.Type != DBCSignalType.ModeDependent)
                                                        lastval = WriteData(obj, canrec.data.Timestamp, rec.VariableData, ref LastTimestamp[rec.header.UID], ref TimeOffset[rec.header.UID]);
                                                }
                                            }                                            
                                        }
                                    break;
                                case RecordType.CanError:
                                    break;
                                case RecordType.LinTrace:
                                    RecLinTrace linrec = rec as RecLinTrace;
                                    if (settings.SignalsDatabase != null)
                                        if (FindMessageFrameID(linrec, out int id))
                                        {
                                            ExportLdfMessage busMsg = settings.SignalsDatabase.ldfCollection[id];
                                            for (int i = 0; i < busMsg.Signals.Count; i++)
                                            {
                                                var obj = ddata.Object(busMsg.Signals[i], (2u << 30) | ((uint)i << 16) | (uint)id);
                                                obj.BusChannel = "LIN";
                                                WriteData(obj, linrec.data.Timestamp, rec.VariableData, ref LastTimestamp[rec.header.UID], ref TimeOffset[rec.header.UID]);
                                            }
                                        }
                                    break;
                                case RecordType.MessageData:
                                    if (Exportable(rec.LinkedBin))
                                    {
                                        ddata.Object(rec.LinkedBin).BusChannel = $"CAN{rec.BusChannel}";
                                        WriteData(ddata.Object(rec.LinkedBin), (rec as RecMessage).data.Timestamp, rec.VariableData, ref LastTimestamp[rec.header.UID], ref TimeOffset[rec.header.UID]);
                                    }
                                    break;
                                default:
                                    break;
                            }
                            if (bigGapFound)
                                goto finalize;
                        }
                        settings.ProgressCallback?.Invoke((int)dr.GetProgress);
                    }
                finalize:
                    if (settings.ProcessingRules is not null)
                        ddata.FinishWrite((LastTimestamp.Zip(TimeOffset, (t, o) => t + o).Max() - FileTimestamp) * TimestampCoeff);
                }

                ddata.SortByIdentifier();
                settings.ProgressCallback?.Invoke(100);
                return ddata;
            }
            catch (Exception e)
            {
                return null;
            }
        }

        internal void ProcessTraceRecords(Action<TraceCollection> ProcessCallback, Action<object> ProgressCallback = null)
        {
            if (ProcessCallback is null)
                return;
            if (Count == 0)
                return;

            UInt32 TimePrecison = Config[BinConfig.BinProp.TimeStampPrecision];

            double FileTimestamp = double.NaN;
            double LastTimestampCan = 0;
            double TimeOffsetCan = 0;
            double LastTimestampCanError = 0;
            double TimeOffsetCanError = 0;
            double LastTimestampLin = 0;
            double TimeOffsetLin = 0;
            UInt32 InitialTimestamp = 0;

            void TraceAdd(TraceCollection tc, ref double LastTimestamp, ref double TimeOffset)
            {
                for (int i = 0; i < tc.Count; i++)
                {
                    if (Double.IsNaN(FileTimestamp))
                        FileTimestamp = (InitialTimestamp == 0 ? FirstTimestamp : InitialTimestamp) * TimestampCoeff;
                    //FileTimestamp = (InitialTimestamp == 0 ? LowestTimestamp : Math.Min(LowestTimestamp, InitialTimestamp)) * TimestampCoeff;

                    if ((tc[i] as IRecordTimeAdapter).FloatTimestamp < LastTimestamp)
                        TimeOffset += (double)TimeRoll * TimePrecison * 0.000001;
                    LastTimestamp = (tc[i] as IRecordTimeAdapter).FloatTimestamp;
                    (tc[i] as IRecordTimeAdapter).FloatTimestamp -= FileTimestamp;
                    (tc[i] as IRecordTimeAdapter).FloatTimestamp += TimeOffset;
                }

                ProcessCallback(tc);
            }

            ProgressCallback?.Invoke(0);
            ProgressCallback?.Invoke("Processing trace data...");
            Type readerType = AttachedLoggers is null ? typeof(RXDataReader) : typeof(RXDataSyncReader);
            using (var dr = (RXDataReader)Activator.CreateInstance(readerType, this))
            {
                InitialTimestamp = dr.GetFilePreBufferInitialTimestamp;
                while (dr.ReadNext())
                {
                    foreach (RecBase rec in dr.Messages)
                        switch (rec.LinkedBin.RecType)
                        {
                            case RecordType.Unknown:
                                break;
                            case RecordType.CanTrace:
                                TraceAdd(rec.ToTraceRow(TimePrecison), ref LastTimestampCan, ref TimeOffsetCan);
                                break;
                            case RecordType.CanError:
                                TraceAdd(rec.ToTraceRow(TimePrecison), ref LastTimestampCanError, ref TimeOffsetCanError);
                                break;
                            case RecordType.LinTrace:
                                TraceAdd(rec.ToTraceRow(TimePrecison), ref LastTimestampLin, ref TimeOffsetLin);
                                break;
                            case RecordType.PreBuffer:
                                ProcessCallback?.Invoke(rec.ToTraceRow(TimePrecison));
                                break;
                            case RecordType.MessageData:
                                break;
                            default:
                                break;
                        }
                    ProgressCallback?.Invoke((int)dr.GetProgress);
                }
            }

            ProgressCallback?.Invoke(100);
        }

        internal TraceCollection ToTraceList(Action<object> ProgressCallback = null)
        {
            TraceCollection TraceList = new TraceCollection();
            TraceList.StartLogTime = DatalogStartTime;
            ProcessTraceRecords((tc) => TraceList.AddRange(tc), ProgressCallback);
            return TraceList;
        }

        public bool ToXML(string xmlFileName)
        {
            XElement CreateBlock(XmlHandler xml, BinBase bin)
            {
                if (bin is null)
                    return null;
                object[] xmlAttr = new object[1] {
                     new XAttribute("UID", bin.header.uniqueid)

                };
                if (bin.external.ContainsKey("X"))
                {
                    xmlAttr = new object[3] {
                     new XAttribute("UID", bin.header.uniqueid),
                     new XAttribute("X", bin.external["X"]),
                     new XAttribute("Y", bin.external["Y"])
                    };
                }
                XElement xblock = xml.NewElement(bin.header.type.ToString().ToUpper(), xmlAttr);
                XmlSchemaComplexType xsdBinType = xml.xsdNodeType(xblock);

                foreach (KeyValuePair<string, PropertyData> prop in bin.data.Union(bin.external).Where(p => p.Value.XmlSequenceGroup == string.Empty))
                    if (!bin.data.isHelperProperty(prop.Value.Name))
                    {
                        if (prop.Value.PropType.IsArray)
                        {
                            // Check if it is sequence
                            XmlSchemaElement xsdPropType = xml.xsdObjectProperty(xsdBinType, prop.Value.Name + "_LIST");
                            if (xsdPropType is not null)
                            {
                                XElement seqblock = xml.NewElement(prop.Value.Name + "_LIST");
                                xblock.Add(seqblock);
                                foreach (var el in prop.Value.Value as Array)
                                    seqblock.Add(xml.NewElement(prop.Value.Name, el));
                            }
                            else
                            {
                                xsdPropType = xml.xsdObjectProperty(xsdBinType, prop.Value.Name);
                                if (xsdPropType is not null)
                                {
                                    if (xsdPropType.SchemaTypeName.Name == "hexBinary")
                                        xblock.Add(xml.NewElement(prop.Value.Name, xml.ToHexBytes(prop.Value.Value)));
                                }
                            }
                            /*XElement arr = new XElement(prop.Value.Name);
                            for (int i = 0; i < prop.Value.SubElementCount.Value; i++)
                                arr.Add(new XElement("SubElement", new XAttribute("ID", i + 1), prop.Value.Value[i]));
                            xblock.Add(arr);*/
                        }
                        else
                        {
                            XmlSchemaElement xsdPropType = xml.xsdObjectProperty(xsdBinType, prop.Value.Name);
                            if (xsdPropType is null)
                                continue;

                            if (xsdPropType.MinOccurs == 0 && prop.Value.IsDefault)
                                continue;

                            xblock.Add(xml.NewElement(prop.Value.Name, prop.Value.Value));
                        }
                    }

                // XML Sequence grouping
                Dictionary<string, PropertyData[]> SequenceGroups =
                    bin.data.Union(bin.external).
                    Where(p => p.Value.XmlSequenceGroup != string.Empty).
                    GroupBy(p => p.Value.XmlSequenceGroup).
                    ToDictionary(p => p.Key, p => p.Where(pf => pf.Value.PropType.IsArray).Select(s => s.Value).ToArray());
                foreach (var seq in SequenceGroups)
                {
                    // Check if it is sequence
                    XmlSchemaElement xsdPropType = xml.xsdObjectProperty(xsdBinType, seq.Key + "_LIST");
                    if (xsdPropType is null)
                        continue;

                    int seqlen = seq.Value.Min(s => (s.Value as Array).Length);
                    if (seqlen == 0)
                        continue;
                    XElement xmlSeqListBlock = xml.NewElement(seq.Key + "_LIST");
                    xblock.Add(xmlSeqListBlock);
                    for (int i = 0; i < seqlen; i++)
                    {
                        XElement xmlSeqEl = xml.NewElement(seq.Key);
                        xmlSeqListBlock.Add(xmlSeqEl);
                        foreach (var prop in seq.Value)
                            xmlSeqEl.Add(xml.NewElement(prop.Name, prop.Value[i]));
                    }
                }

                return xblock;
            }

            try
            {
                using (XmlHandler xml = new XmlHandler(xmlFileName))
                {
                    xml.CreateRoot("1.0.1",
                        new XElement[]
                        {
                            CreateBlock(xml, Config),
                            CreateBlock(xml, ConfigFTP),
                            CreateBlock(xml, ConfigMobile),
                            CreateBlock(xml, ConfigS3),
                            CreateBlock(xml, ConfigWiFi),
                        });

                    XElement groupNode;
                    foreach (var binGroup in this.GroupBy(x => x.Value.BinType))
                    {
                        groupNode = xml.AddGroupNode(binGroup.Key.ToString());
                        foreach (var bin in binGroup)
                            groupNode.Add(CreateBlock(xml, bin.Value));
                    }

                    xml.Save();
                }
                return true;
            }
            catch (Exception exc)
            {

                return false;
            }
        }

        private protected bool ReadXmlContent(XmlHandler xml)
        {
            BinBase ReadBin(XmlHandler xml, XElement node)
            {
                if (node is null)
                    return null;

                BinHeader hs = new BinHeader();
                if (!Enum.TryParse(node.Name.LocalName, true, out hs.type))
                    return null;

                if (!UInt16.TryParse(node.Attribute("UID").Value, out hs.uniqueid))
                    return null;

                BinBase bin = (BinBase)Activator.CreateInstance(BinBase.BlockInfo[hs.type], hs);
                XmlSchemaComplexType xsdBinType = xml.xsdNodeType(node);

                if (node.Attribute("X") is not null)
                    if (uint.TryParse(node.Attribute("X").Value, out uint X))
                        bin.external.AddProperty("X", typeof(uint), X);
                if (node.Attribute("Y") is not null)
                    if (uint.TryParse(node.Attribute("Y").Value, out uint Y))
                        bin.external.AddProperty("Y", typeof(uint), Y);
                foreach (var prop in bin.data.Where(p => p.Value.XmlSequenceGroup == string.Empty))
                {
                    if (prop.Value.PropType.IsArray)
                    {
                        // Check if it is sequence
                        XmlSchemaElement xsdPropType = xml.xsdObjectProperty(xsdBinType, prop.Value.Name + "_LIST");
                        if (xsdPropType is not null)
                        {
                            XElement propEl = XmlHandler.Child(node, prop.Value.Name + "_LIST");
                            if (propEl == null)
                                continue;

                            var converter = TypeDescriptor.GetConverter(prop.Value.PropType.GetElementType());
                            var arrElements = XmlHandler.Childs(propEl, prop.Value.Name);
                            var arrProp = Activator.CreateInstance(prop.Value.PropType, arrElements.Count());
                            for (int i = 0; i < arrElements.Count(); i++)
                                (arrProp as Array).SetValue(converter.ConvertFrom(arrElements.ElementAt(i).Value), i);
                            prop.Value.Value = arrProp;
                        }
                        else
                        {
                            XElement propEl = XmlHandler.Child(node, prop.Value.Name);
                            if (propEl == null)
                                continue;

                            xsdPropType = xml.xsdObjectProperty(xsdBinType, prop.Value.Name);
                            if (xsdPropType is not null)
                            {
                                if (xsdPropType.SchemaTypeName.Name == "hexBinary")
                                    prop.Value.Value = Bytes.FromHexBinary(propEl.Value);
                            }
                        }
                    }
                    else if (prop.Value.PropType == typeof(IPAddress))
                    {
                        XElement propEl = XmlHandler.Child(node, prop.Value.Name);
                        if (propEl == null)
                            continue;
                        prop.Value.Value = IPAddress.Parse(propEl.Value);
                    }
                    else
                    {
                        XElement propEl = XmlHandler.Child(node, prop.Value.Name);
                        if (propEl == null)
                            continue;

                        if (prop.Value.PropType == typeof(Single) || prop.Value.PropType == typeof(Double))
                        {
                            float.TryParse(propEl.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out float result);
                            prop.Value.Value = result;
                        }
                        else
                        {
                            var converter = TypeDescriptor.GetConverter(prop.Value.PropType);
                            prop.Value.Value = converter.ConvertFrom(propEl.Value);
                        }
                    }                    
                }

                // XML Sequence grouping
                Dictionary<string, PropertyData[]> SequenceGroups = bin.data.
                    Where(p => p.Value.XmlSequenceGroup != string.Empty).
                    GroupBy(p => p.Value.XmlSequenceGroup).
                    ToDictionary(p => p.Key, p => p.Where(pf => pf.Value.PropType.IsArray).Select(s => s.Value).ToArray());
                foreach (var seq in SequenceGroups)
                {
                    // Check if it is sequence
                    XmlSchemaElement xsdPropType = xml.xsdObjectProperty(xsdBinType, seq.Key + "_LIST");
                    if (xsdPropType is null)
                        continue;

                    XElement xmlSeqListBlock = XmlHandler.Child(node, seq.Key + "_LIST");
                    if (xmlSeqListBlock is null)
                        continue;

                    var arrElements = XmlHandler.Childs(xmlSeqListBlock, seq.Key);
                    int seqlen = arrElements.Count();
                    foreach (var prop in seq.Value)
                    {
                        var converter = TypeDescriptor.GetConverter(prop.PropType.GetElementType());
                        var arrProp = Activator.CreateInstance(prop.PropType, seqlen);

                        for (int i = 0; i < seqlen; i++)
                        {
                            XElement propEl = XmlHandler.Child(arrElements.ElementAt(i), prop.Name);
                            if (propEl is not null)
                            {
                                if (converter is SingleConverter && CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator == ",")
                                {
                                    if (Single.TryParse(propEl.Value, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out Single floatValue))
                                        (arrProp as Array).SetValue(floatValue, i);
                                    else
                                        (arrProp as Array).SetValue(0, i);
                                }
                                else
                                {
                                    (arrProp as Array).SetValue(converter.ConvertFrom(propEl.Value), i);
                                }
                            }
                        }
                        prop.Value = arrProp;
                    }
                    seq.Value[0].SubElementCount.Value = seqlen;
                }

                var extProps = node.Elements().Where(n => !n.Name.LocalName.EndsWith("_LIST") && !bin.data.ContainsKey(n.Name.LocalName));
                foreach (var prop in extProps)
                {
                    bin.external.AddProperty(prop.Name.LocalName, typeof(string));
                    bin.external[prop.Name.LocalName] = prop.Value;
                }

                return bin;
            }

            BinBase ReadConfigBin(XmlHandler xml, Blocks.BlockType blocktype) => ReadBin(xml, XmlHandler.Child(xml.rootNode, blocktype.ToString().ToUpper()));

            if (xml.TryLoadXML(out Error))
            {
                Config = (BinConfig)ReadBin(xml, xml.configNode);
                ConfigFTP = (BinConfigFTP)ReadConfigBin(xml, Blocks.BlockType.Config_Ftp);
                ConfigMobile = (BinConfigMobile)ReadConfigBin(xml, Blocks.BlockType.Config_Mobile);
                ConfigS3 = (BinConfigS3)ReadConfigBin(xml, Blocks.BlockType.CONFIG_S3);
                ConfigWiFi = (BinConfigWiFi)ReadConfigBin(xml, Blocks.BlockType.Config_WiFi);


                foreach (XElement group in xml.blocksNode.Elements())
                    foreach (XElement bin in group.Elements())
                    {
                        Add(ReadBin(xml, bin));

                    }

                return true;
            }
            return false;
        }

        public bool ReadXMLStructure(Stream xmlData, Stream xsdData)
        {
            using (XmlHandler xml = new XmlHandler(xmlData, xsdData))
                return ReadXmlContent(xml);
        }

        public bool ReadXMLStructure(string xmlFileName)
        {
            using (XmlHandler xml = new XmlHandler(xmlFileName))
                return ReadXmlContent(xml);
        }

    }
}
