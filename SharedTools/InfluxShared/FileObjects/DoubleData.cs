using InfluxShared.Helpers;
using InfluxShared.Interfaces;
using InfluxShared.Objects;
using System;
using System.IO;

namespace InfluxShared.FileObjects
{
    public enum StorageCacheType : byte { Disk, Memory }

    public class DoubleData : IDisposable
    {
        internal readonly IStorage<double> TimeStream = null;
        internal readonly IStorage<double> DataStream = null;
        internal BinaryData BinaryHelper = null;
        private DataTransformer Transformer = null;
        internal double ReadProgress = 0;

        public UInt64 identifier = 0;
        public string ChannelName { get; set; }
        public string ChannelUnits { get; set; }
        public string BusChannel { get; set; } = "";

        public UInt32 RecordCount = 0;

        public double TempTime = double.NaN;
        public double TempData = double.NaN;
        public bool TempEof = false;

        private bool disposedValue;

        public Action<double, double> WriteData;

        internal DoubleData(StorageCacheType StorageCache, string TempPath = null)
        {
            ChannelName = "";
            ChannelUnits = "";

            switch (StorageCache)
            {
                case StorageCacheType.Disk:
                    if (TempPath is null)
                        TempPath = PathHelper.TempPath;

                    TimeStream = new DiskStorage<double>(DiskStorage<double>.GenerateFileName(TempPath, "time"));
                    DataStream = new DiskStorage<double>(DiskStorage<double>.GenerateFileName(TempPath, "data"));
                    break;
                case StorageCacheType.Memory:
                    TimeStream = new MemoryStorage<double>();
                    DataStream = new MemoryStorage<double>();
                    break;
            }
            WriteData = WriteDataToStorage;
        }

        #region Destructors
        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    if (TimeStream != null)
                    {
                        (TimeStream as FileStream).Close();

                    }

                    if (DataStream != null)

                        (DataStream as FileStream).Close();
                }

                // TODO: free unmanaged resources (unmanaged objects) and override finalizer
                // TODO: set large fields to null
                disposedValue = true;
            }
        }

        // // TODO: override finalizer only if 'Dispose(bool disposing)' has code to free unmanaged resources
        // ~DoubleData()
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

        public void InjectTransformer(ProcessingRules rules)
        {
            Transformer = new DataTransformer(rules)
            {
                Writer = WriteDataToStorage,
            };
            WriteData = Transformer.Push;
        }

        internal void FinishWrite(double EndTime)
        {
            Transformer?.PushEnd(EndTime);
        }

        public void WriteDataToStorage(double Timestamp, double DoubleValue)
        {
            TimeStream.Write(Timestamp);
            DataStream.Write(DoubleValue);
            RecordCount++;
            //TimeStream.Flush();
            //DataStream.Flush();
        }

        public double WriteBinaryData(double Timestamp, byte[] BinaryArray)
        {
            if (!BinaryHelper.ExtractHex(BinaryArray, out BinaryData.HexStruct hex))
                return double.NaN;

            double DoubleValue = BinaryHelper.CalcValue(ref hex);
            WriteData(Timestamp, DoubleValue);
            return DoubleValue;
        }

        public void Copy(Stream output, Int64[] outOffsets)
        {
            if (outOffsets == null)
                return;

            if (outOffsets.Length < 2)
                return;

            const int maxBufferSize = 5 * 0x100000; // 5 MB

            byte[] buffer = new byte[maxBufferSize];
            int buffSize;

            void CopyStream(Stream sInput, Stream sOutput, Int64 OutputPos)
            {
                sInput.Seek(0, SeekOrigin.Begin);
                sOutput.Seek(OutputPos, SeekOrigin.Begin);
                while ((buffSize = sInput.Read(buffer, 0, maxBufferSize)) > 0)
                    sOutput.Write(buffer, 0, buffSize);
            }


            CopyStream(TimeStream as Stream, output, outOffsets[0]);
            CopyStream(DataStream as Stream, output, outOffsets[1]);
        }

        public void InitReading()
        {
            TimeStream.InitRead();
            DataStream.InitRead();

            TempEof = false;
            ReadNext();
        }

        public bool ReadNext(bool UpdateProgress = true)
        {
            void SetEOF()
            {
                TempTime = double.NaN;
                TempData = double.NaN;
                TempEof = true;
            }

            try
            {
                if (!TimeStream.Read(ref TempTime) || !DataStream.Read(ref TempData))
                {
                    SetEOF();
                    return false;
                }

                return true;
            }
            catch
            {
                SetEOF();
                return false;
            }
            finally
            {
                if (UpdateProgress)
                    ReadProgress = TimeStream.Length == 0 ? 1 : TimeStream.Position / (double)TimeStream.Length;
            }
        }

    }
}
