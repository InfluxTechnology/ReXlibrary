using RXD.Blocks;
using RXD.DataRecords;
using SharedObjects;
using System;
using System.Collections.Generic;

namespace RXD.Base.FrameCollectors
{
    internal class J1939FrameCollector : IFrameCollector
    {
        internal class MultiFrameData : RecordCollection
        {
            public UInt32 PGN;
            public UInt32 Source;
            public UInt16 MessageSize;
            public byte FrameCount;

            public bool isCompleted => Count == FrameCount + 1;

            public RecCanTrace PackJ1939Message()
            {
                RecCanTrace rec = new RecCanTrace
                {
                    header = new RecHeader()
                    {
                        UID = this[0].header.UID,
                        InfSize = this[0].header.InfSize,
                        DLC = (byte)MessageSize
                    },
                    LinkedBin = this[0].LinkedBin,
                    BusChannel = this[0].BusChannel,
                    NotExportable = true,
                    CustomType = PackType.J1939,
                };

                rec.data.Timestamp = (this[FrameCount] as RecCanTrace).data.Timestamp;
                rec.data.Flags = (this[0] as RecCanTrace).data.Flags;
                rec.data.CanID = (this[0] as RecCanTrace).data.CanID;
                rec.data.CanID.PGN = PGN;

                rec.VariableData = new byte[rec.header.DLC];

                for (int i = 1; i <= FrameCount; i++)
                    Array.Copy(this[i].VariableData, 1, rec.VariableData, (i - 1) * 7, Math.Min(7, MessageSize - (i - 1) * 7));

                return rec;
            }
        }

        Dictionary<UInt32, MultiFrameData> FrameCollection = new();

        public MultiFrameData GetJ1939(CanIdentifier ident) => FrameCollection.TryGetValue(ident.Source, out MultiFrameData data) ? data : null;

        public MultiFrameData AddJ1939(RecCanTrace msg)
        {
            MultiFrameData data = new MultiFrameData()
            {
                PGN = (UInt32)(msg.VariableData[5] | msg.VariableData[6] << 8 | msg.VariableData[7] << 16),
                Source = msg.data.CanID.Source,
                MessageSize = BitConverter.ToUInt16(msg.VariableData, 1),
                FrameCount = msg.VariableData[3]
            };

            FrameCollection.Add(data.Source, data);

            return data;
        }

        public MultiFrameData AddOrGetJ1939(RecCanTrace msg)
        {
            MultiFrameData data;
            if (FrameCollection.TryGetValue(msg.data.CanID.Source, out data))
                return data;

            data = new MultiFrameData()
            {
                PGN = (UInt32)(msg.VariableData[5] | msg.VariableData[6] << 8 | msg.VariableData[7] << 16),
                Source = msg.data.CanID.Source,
                MessageSize = BitConverter.ToUInt16(msg.VariableData, 1),
                FrameCount = msg.VariableData[3]
            };

            if (data.FrameCount * 7 + 1 < data.MessageSize)
                return null;

            FrameCollection.Add(data.Source, data);

            return data;
        }

        public bool TryCollect(RecBase record, RXDataReader reader)
        {
            if (record.LinkedBin is null)
                return false;

            if (record.LinkedBin.BinType != BlockType.CANMessage || record.LinkedBin.RecType != RecordType.CanTrace)
                return false;

            RecCanTrace msg = record as RecCanTrace;

            //if (msgBlock[BinCanMessage.BinProp.isJ1939] == true)
            if ((msg.data.Flags & J1939.pgnFlagsMask) != J1939.pgnFlagsValue)
                return false;

            if (J1939.isPgnConnectMessage(msg))
            {
                MultiFrameData data = AddOrGetJ1939(msg);
                if (data is not null)
                {
                    if (data.Count > 0)
                    {
                        //MessageBox.Show("Previous buffer untriggered");
                    }
                    data.Clear();
                    data.Add(msg);

                    return true;
                }
            }
            else if (J1939.isPgnDataTransferMessage(msg))
            {
                MultiFrameData data = GetJ1939(msg.data.CanID);
                if (data != null)
                {
                    data.Add(msg);

                    if (data.isCompleted)
                    {
                        reader.MessageCollection.Insert(reader.MessageCollection.IndexOf(record) + 1, data.PackJ1939Message());
                        data.Clear();
                    }

                    return true;
                }
            }

            return false;
        }

    }
}