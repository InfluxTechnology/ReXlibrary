using RXD.Blocks;
using RXD.DataRecords;
using System;

namespace RXD.Base.FrameCollectors
{
    internal class ModeFrameCollector : IFrameCollector
    {
        static MessageFlags FlagsMask = MessageFlags.IDE | MessageFlags.EDL | MessageFlags.BRS | MessageFlags.SRR | MessageFlags.DIR;
        static MessageFlags FlagsTxValue = MessageFlags.DIR;
        static MessageFlags FlagsRxValue = 0;

        static byte FrameTypeMask = 0xF0;
        static byte FrameSequenceIdMask = 0x0F;
        static byte SingleFrame = 0x00;
        static byte FirstFrame = 0x10;
        static byte ConsecutiveFrame = 0x20;
        static byte FlowControlFrame = 0x30;

        static byte ModeRespSID = 0x40;
        static byte NegativeResponse = 0x7F;

        internal class MultiFrameData : RecordCollection
        {
            public UInt32 Ident;
            public byte Mode;
            internal byte CanLengthSize;
            internal int DataSize;
            internal int TargetCount;
            internal RecCanTrace UDS;

            public void Init()
            {
                Clear();
                Ident = 0;
                TargetCount = 0;
                DataSize = 0;
                UDS = null;
            }

            public RecCanTrace CreateUDSRec()
            {
                RecCanTrace rec = new()
                {
                    header = new RecHeader()
                    {
                        UID = this[0].header.UID,
                        InfSize = this[0].header.InfSize,
                        DLC = (byte)DataSize
                    },
                    LinkedBin = this[0].LinkedBin,
                    BusChannel = this[0].BusChannel,
                    NotExportable = true,
                    NotVisible = true,
                    CustomType = PackType.UDS,
                };

                rec.data.Timestamp = (this[Count - 1] as RecCanTrace).data.Timestamp;
                rec.data.Flags = (this[0] as RecCanTrace).data.Flags;
                rec.data.CanID = (this[0] as RecCanTrace).data.CanID;

                return rec;
            }
        }

        internal class MultiFrameObj
        {
            public byte Mode = 0;
            public MultiFrameData tx;
            public MultiFrameData rx;

            UInt16 OutOffset = 0;
            UInt16 InOffset = 0;

            UInt16 Copy(Array sourceArray, int sourceIndex, Array destinationArray, int length)
            {
                Array.Copy(sourceArray, sourceIndex, destinationArray, OutOffset, length);
                OutOffset += (UInt16)length;
                return (UInt16)length;
            }

            public RecCanTrace PackTxUDS()
            {
                RecCanTrace rec = tx.CreateUDSRec();

                OutOffset = 0;
                InOffset = 0;

                tx.CanLengthSize = (byte)(tx.Count > 1 ? 2 : 1);
                if (Mode == 0x22)
                    rec.CustomType = PackType.UDS22;
                else if (Mode == 0x23)
                    rec.CustomType = PackType.UDS23;

                rec.VariableData = new byte[rec.header.DLC];
                InOffset += Copy(tx[0].VariableData, tx.CanLengthSize, rec.VariableData, Math.Min(tx.DataSize - InOffset, (UInt16)(8 - tx.CanLengthSize)));

                for (int i = 1; i < tx.Count; i++)
                    InOffset += Copy(tx[i].VariableData, 1, rec.VariableData, Math.Min(7, tx.DataSize - InOffset));

                return rec;
            }

            public RecCanTrace PackRxUDS()
            {
                RecCanTrace rec = rx.CreateUDSRec();

                OutOffset = 0;
                InOffset = 0;

                UInt16 txItemSize = 0;
                rx.CanLengthSize = (byte)(rx.Count > 1 ? 2 : 1);
                if (Mode == 0x22)
                {
                    rec.CustomType = PackType.UDS22;
                    rec.header.DLC += 2;
                    rec.VariableData = new byte[rec.header.DLC];
                    InOffset += Copy(rx[0].VariableData, rx.CanLengthSize, rec.VariableData, 3);
                    rec.VariableData[3] = 0;
                    rec.VariableData[4] = 0;
                    OutOffset += 2;
                    InOffset += Copy(rx[0].VariableData, rx.CanLengthSize + 3, rec.VariableData, Math.Min(rx.DataSize - InOffset, (UInt16)(5 - rx.CanLengthSize)));
                }
                else if (Mode == 0x23)
                {
                    txItemSize = (ushort)(tx[0].VariableData[tx.CanLengthSize + 1] & 0x0F);
                    rec.CustomType = PackType.UDS23;
                    rec.header.DLC += (byte)txItemSize;
                    rec.VariableData = new byte[rec.header.DLC];
                    InOffset += Copy(rx[0].VariableData, rx.CanLengthSize, rec.VariableData, 1);
                    Copy(tx[0].VariableData, tx.CanLengthSize + 2, rec.VariableData, txItemSize);
                    InOffset += Copy(rx[0].VariableData, rx.CanLengthSize + 1, rec.VariableData, Math.Min(7 - rx.CanLengthSize, rx.DataSize - InOffset));
                }
                else
                {
                    rec.VariableData = new byte[rec.header.DLC];
                    InOffset += Copy(rx[0].VariableData, rx.CanLengthSize, rec.VariableData, Math.Min(rx.DataSize - InOffset, (UInt16)(8 - rx.CanLengthSize)));
                }

                for (int i = 1; i < rx.Count; i++)
                    InOffset += Copy(rx[i].VariableData, 1, rec.VariableData, Math.Min(7, rx.DataSize - InOffset));

                return rec;
            }
        }

        MultiFrameObj data = new() { tx = new(), rx = new(), Mode = 0 };

        public void Init()
        {
            data.Mode = 0;
            data.tx.Init();
            data.rx.Init();
        }

        public bool AppendTxFrame(RecCanTrace msg, out byte FrameType)
        {
            FrameType = (byte)(msg.VariableData[0] & FrameTypeMask);
            if (FrameType == FlowControlFrame)
                return true;

            // Init
            if (data.rx.Count > 0 || data.tx.UDS is not null)
                Init();
            init:

            if (data.tx.Count == 0)
            {
                if (FrameType == SingleFrame)
                {
                    data.Mode = msg.VariableData[1];
                    data.tx.Ident = msg.data.CanID;
                    data.tx.DataSize = msg.VariableData[0];
                    data.tx.TargetCount = 1;
                }
                else if (FrameType == FirstFrame)
                {
                    data.Mode = msg.VariableData[2];
                    data.tx.Ident = msg.data.CanID;
                    data.tx.DataSize = (ushort)(((msg.VariableData[0] & 0x0F) << 8) + msg.VariableData[1]);
                    data.tx.TargetCount = (data.rx.DataSize + 6) / 7;
                }
                else
                    return false;

                data.tx.Add(msg);
            }
            else
            {
                if (msg.data.CanID != data.tx.Ident)
                {
                    Init();
                    goto init;
                }

                if (FrameType == ConsecutiveFrame && (msg.VariableData[0] & FrameSequenceIdMask) == ((1 + data.tx[data.tx.Count - 1].VariableData[0]) & FrameSequenceIdMask))
                    data.tx.Add(msg);
                else
                    return false;
            }

            return true;
        }

        public bool AppendRxFrame(RecCanTrace msg)
        {
            if (data.tx.Count == 0 || data.tx.UDS is null)
                return false;

            byte FrameType = (byte)(msg.VariableData[0] & FrameTypeMask);
            if (FrameType == FlowControlFrame)
                return true;

            if (data.rx.Count == 0)
            {
                if (FrameType == SingleFrame && msg.VariableData[1] == data.Mode + ModeRespSID)
                {
                    data.rx.Ident = msg.data.CanID;
                    data.rx.DataSize = msg.VariableData[0];
                    data.rx.TargetCount = 1;
                }
                else if (FrameType == FirstFrame && msg.VariableData[2] == data.Mode + ModeRespSID)
                {
                    data.rx.Ident = msg.data.CanID;
                    data.rx.DataSize = (ushort)(((msg.VariableData[0] & 0x0F) << 8) + msg.VariableData[1]);
                    data.rx.TargetCount = (data.rx.DataSize + 6) / 7;
                }
                else
                    return false;

                data.rx.Add(msg);
            }
            else
            {
                if (msg.data.CanID != data.rx.Ident) 
                    return false;

                if (FrameType == ConsecutiveFrame && (msg.VariableData[0] & FrameSequenceIdMask) == ((1 + data.rx[data.rx.Count - 1].VariableData[0]) & FrameSequenceIdMask))
                    data.rx.Add(msg);
                else 
                    return false;
            }

            return true;
        }

        public bool TryCollect(RecBase record, RXDataReader reader)
        {
            if (record.LinkedBin is null)
                return false;

            if (record.LinkedBin.BinType != BlockType.CANMessage || record.LinkedBin.RecType != RecordType.CanTrace)
                return false;

            RecCanTrace msg = record as RecCanTrace;
            byte FrameType;

            if ((msg.data.Flags & FlagsMask) == FlagsTxValue)
            {
                if (AppendTxFrame(msg, out FrameType))
                {
                    if (FrameType != FlowControlFrame && data.tx.Count == data.tx.TargetCount && data.tx.Count > 0)
                        data.tx.UDS = data.PackTxUDS();
                    return true;
                }
            }
            else if ((msg.data.Flags & FlagsMask) == FlagsRxValue)
            {
                if (AppendRxFrame(msg))
                {
                    if (data.rx.Count == data.rx.TargetCount && data.rx.Count > 0)
                    {
                        reader.MessageCollection.Insert(reader.MessageCollection.IndexOf(record) + 1, data.PackRxUDS());
                        Init();
                    }
                    return true;
                }
            }

            return false;
        }

    }
}
