using MDF4xx.Frames;
using RXD.Objects;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace RXD.DataRecords
{
    [Flags]
    public enum LinMessageFlags : byte
    {
        /// <summary>
        /// Direction (Rx, Tx)
        /// </summary>
        DIR = 1 << 0,
        /// <summary>
        /// Lin Parity Error
        /// </summary>
        LPE = 1 << 1,
        /// <summary>
        /// Lin Checksum Error
        /// </summary>
        LCSE = 1 << 2,
        /// <summary>
        /// Lin Transmission Error
        /// </summary>
        LTE = 1 << 3,
        /// <summary>
        /// Lin Transmission Error
        /// </summary>
        Error = LPE | LCSE | LTE,
    }

    internal class RecLinTrace : RecBase
    {
        [StructLayout(LayoutKind.Sequential, Pack = 1, CharSet = CharSet.Ansi)]
        internal class DataRecord
        {
            public UInt32 Timestamp;
            public byte LinID;
            [MarshalAs(UnmanagedType.ByValArray, ArraySubType = UnmanagedType.U1, SizeConst = 3)]
            public byte[] reserved;
            public LinMessageFlags Flags;
        }

        internal new DataRecord data { get => (DataRecord)base.data; set => base.data = value; }

        internal override UInt32 RawTimestamp { get => data.Timestamp; set => data.Timestamp = value; }

        public RecLinTrace()
        {
            data = new DataRecord();
        }

        public override List<BaseDataFrame> ToMdfFrame()
        {
            var frames = base.ToMdfFrame();
            if (NotExportable)
                return frames;

            if ((data.Flags & LinMessageFlags.Error) == 0)
            {
                LIN_DataFrame frame = new LIN_DataFrame();

                // Copy fixed length data
                frame.data.Timestamp = data.Timestamp;
                frame.data.BusChannel = (byte)(BusChannel + 1);
                frame.data.LinID = data.LinID;
                frame.data.DLC = header.DLC;
                //frame.data.DataBytes = header.DLC;
                frame.data.Flags = (byte)((byte)data.Flags & 0x01);

                // Copy variable data
                frame.VariableData = new byte[8];
                Buffer.BlockCopy(VariableData, 0, frame.VariableData, 0, header.DLC);

                frames.Add(frame);
            }
            else
            {
                if (data.Flags.HasFlag(LinMessageFlags.LPE))
                {

                }
                if (data.Flags.HasFlag(LinMessageFlags.LCSE))
                {
                    LIN_ChecksumErrorFrame frame = new();

                    // Copy fixed length data
                    frame.data.Timestamp = data.Timestamp;
                    frame.data.BusChannel = (byte)(BusChannel + 1);
                    frame.data.LinID = data.LinID;
                    frame.data.DLC = header.DLC;
                    //frame.data.CRC = 0;
                    //frame.data.CRCModel = 0xff;
                    frame.data.Flags = (byte)((byte)data.Flags & 0x01);

                    // Copy variable data
                    frame.VariableData = new byte[8];
                    Buffer.BlockCopy(VariableData, 0, frame.VariableData, 0, header.DLC);

                    frames.Add(frame);
                }
                if (data.Flags.HasFlag(LinMessageFlags.LTE))
                {
                    LIN_TransmissionErrorFrame frame = new();

                    // Copy fixed length data
                    frame.data.Timestamp = data.Timestamp;
                    frame.data.BusChannel = (byte)(BusChannel + 1);
                    frame.data.LinID = data.LinID;

                    frames.Add(frame);
                }

            }

            return frames;
        }

        public override MessageFrame ConvertToMdfMessageFrame(UInt16 GroupID, byte DLC)
        {
            if ((data.Flags & LinMessageFlags.Error) != 0)
                return null;

            MessageFrame frame = new MessageFrame();

            // Copy fixed length data
            frame.data.Timestamp = data.Timestamp;
            frame.data.Type = (FrameType)GroupID;

            // Copy variable data
            frame.VariableData = new byte[DLC];
            Buffer.BlockCopy(VariableData, 0, frame.VariableData, 0, Math.Min(DLC, header.DLC));

            return frame;
        }

        public override TraceCollection ToTraceRow(UInt32 TimestampPrecision)
        {
            var frames = base.ToTraceRow(TimestampPrecision);

            TraceLin trace = new TraceLin()
            {
                //TraceType = LinkedBin.RecType,
                RawTimestamp = data.Timestamp,
                FloatTimestamp = (double)data.Timestamp * TimestampPrecision * 0.000001,
                BusChannel = BusChannel,
                NotExportable = NotExportable,
                LinID = data.LinID,
                flagDIR = data.Flags.HasFlag(LinMessageFlags.DIR),
                flagLPE = data.Flags.HasFlag(LinMessageFlags.LPE),
                flagLCSE = data.Flags.HasFlag(LinMessageFlags.LCSE),
                flagLTE = data.Flags.HasFlag(LinMessageFlags.LTE),
                DLC = header.DLC,
                Data = new byte[header.DLC]
            };

            // Copy variable data
            Buffer.BlockCopy(VariableData, 0, trace.Data, 0, header.DLC);

            frames.Add(trace);

            return frames;
        }
    }
}
