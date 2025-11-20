using RXD.Objects;
using System;
using System.Runtime.InteropServices;

namespace RXD.DataRecords
{
    internal class RecPreBuffer : RecBase
    {
        [StructLayout(LayoutKind.Sequential, Pack = 1, CharSet = CharSet.Ansi)]
        internal class DataRecord
        {
            public UInt32 Timestamp;
            public UInt32 PreStartSector;
            public UInt32 PreCurrentSector;
            public UInt32 PreEndSector;
            public UInt32 PostStartSector;
            public UInt32 PostEndSector;
            public UInt32 NextPreBufferSector;
            public UInt32 InitialTimestamp;

            public UInt32 PreSize => PreEndSector - PreStartSector;
            public bool ContainPreBufferInfo => PreStartSector > 0 && PreCurrentSector > 0 && PreEndSector > 0;
            public bool ContainPostBufferInfo => PostStartSector > 0 && PostEndSector > 0;
            public bool ContainPostData => ContainPostBufferInfo || Timestamp > 0;
            public bool isLast => NextPreBufferSector == 0;
        }

        internal new DataRecord data { get => (DataRecord)base.data; set => base.data = value; }

        internal override UInt32 RawTimestamp { get => data.Timestamp; set => data.Timestamp = value; }

        private bool RelativeSectors = false;

        public RecPreBuffer()
        {
            data = new DataRecord();
        }

        public void FixOffsetBy(UInt32 SectorOffset)
        {
            if (RelativeSectors)
                return;

            RelativeSectors = true;

            if (data.ContainPreBufferInfo)
            {
                data.PreStartSector -= SectorOffset;
                data.PreCurrentSector -= SectorOffset;
                data.PreEndSector -= SectorOffset;
                //if (data.PreCurrentSector < data.PreEndSector)
                //data.PreCurrentSector += 1;
            }

            if (data.ContainPostBufferInfo)
            {
                data.PostStartSector -= SectorOffset;
                data.PostEndSector -= SectorOffset;
            }

            if (!data.isLast)
                data.NextPreBufferSector -= SectorOffset;
        }

        public override TraceCollection ToTraceRow(UInt32 TimestampPrecision)
        {
            var frames = base.ToTraceRow(TimestampPrecision);

            if (header.InfSize == 4 && header.DLC == 1)
            {
                TraceEvent tevent = new TraceEvent()
                {
                    //TraceType = RecordType.PreBuffer,
                    RawTimestamp = data.Timestamp,
                    FloatTimestamp = (double)data.Timestamp * TimestampPrecision * 0.000001,
                    NotExportable = NotExportable,
                    EventTriggerName = LinkedBin.Name,
                    Value = VariableData[0]
                };

                // Copy variable data
                //Buffer.BlockCopy(VariableData, 0, trace._Data, 0, header.DLC);

                frames.Add(tevent);

            }
            /*else if (data.Timestamp > 0)
            {
                TracePreBuffer trace = new TracePreBuffer()
                {
                    //TraceType = RecordType.PreBuffer,
                    RawTimestamp = data.Timestamp,
                    FloatTimestamp = (double)data.Timestamp * TimestampPrecision * 0.000001,
                    NotExportable = NotExportable,
                    //DLC = header.DLC,
                    //_Data = new byte[header.DLC]
                    strData = LinkedBin.Name
                };

                // Copy variable data
                //Buffer.BlockCopy(VariableData, 0, trace._Data, 0, header.DLC);

                frames.Add(trace);
            }*/

            return frames;
        }


    }
}
