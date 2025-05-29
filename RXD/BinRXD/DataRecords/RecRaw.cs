using InfluxShared.Helpers;
using RXD.Base;
using RXD.Blocks;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

namespace RXD.DataRecords
{
    public class RecRaw
    {
        internal RecHeader header;
        internal byte[] Data;
        internal byte[] VariableData;

        internal byte BusChannel;
        internal BinBase LinkedBin = null;

        public RecRaw()
        {

        }

        public override string ToString()
        {
            return $"UID: {header.UID}; Infsize: {header.InfSize}; DLC: {header.DLC};    Data: " + BitConverter.ToString(Data) + ";   VariableData: " + BitConverter.ToString(VariableData) + "\r\n";
        }

        public UInt32 ExtractRawTimestamp => BitConverter.ToUInt32(Data, 0);

        private static List<RecRaw> ReadList(ref IntPtr src)
        {
            List<RecRaw> records = new List<RecRaw>() 
            { 
                new RecRaw(), 
                new RecRaw() 
            };

            for (int i = 0; i < records.Count; i++)
            {
                records[i].header = new RecHeader();
                Marshal.PtrToStructure(src, records[i].header);
            }
            src += Marshal.SizeOf(records[0].header);

            for (int i = 0; i < records.Count; i++)
            {
                records[i].Data = new byte[records[i].header.InfSize];
                Marshal.Copy(src, records[i].Data, 0, records[i].header.InfSize);
            }
            src += records[0].header.InfSize;

            byte dlc = (byte)((records[0].header.DLC / 2) /*- BinRXD.InternalTimestampSize*/);

            for (int i = 0; i < records.Count; i++)
            {
                records[i].VariableData = new byte[records[i].header.DLC];
                Marshal.Copy(src, records[i].VariableData, 0, records[i].header.DLC);
            }
            src += records[0].header.DLC;

            // Fix DLC and create multiple records
            for (int i = 0; i < records.Count; i++)
            {
                records[i].header.DLC = dlc;
                for (int j = 0; j < 4; j++)
                    records[i].Data[j] = 0;
                Array.Copy(records[i].VariableData, i * dlc, records[i].Data, 0, BinRXD.InternalTimestampSize);
                records[i].VariableData = records[i].VariableData.Skip(i * dlc).Take(dlc).ToArray();
            }

            return records;
        }

        public static List<RecRaw> Read(ref IntPtr src)
        {
            if (BinRXD.InternalTimestamp)
                return ReadList(ref src);

            RecRaw rec = new RecRaw();
            
            rec.header = new RecHeader();
            Marshal.PtrToStructure(src, rec.header);
            src += Marshal.SizeOf(rec.header);

            rec.Data = new byte[rec.header.InfSize];
            Marshal.Copy(src, rec.Data, 0, rec.header.InfSize);
            src += rec.header.InfSize;

            rec.VariableData = new byte[rec.header.DLC];
            Marshal.Copy(src, rec.VariableData, 0, rec.header.DLC);
            src += rec.header.DLC;

            return new List<RecRaw>() { rec };
        }

        public static void ApplyTimestampOffset(ref IntPtr src, Int64 Offset)
        {
            RecHeader hdr = new RecHeader();
            Marshal.PtrToStructure(src, hdr);
            src += Marshal.SizeOf(hdr);

            UInt32 tmpTime = (UInt32)(Marshal.PtrToStructure<UInt32>(src) + Offset);
            Marshal.StructureToPtr(tmpTime, src, false);

            src += hdr.InfSize + hdr.DLC;
        }

        /*public static RecRaw Read(BinaryReader br)
        {
            RecRaw rec = new RecRaw();

            rec.header = br.ReadBytes(Marshal.SizeOf(typeof(RecHeader))).ConvertTo<RecHeader>();
            rec.Data = br.ReadBytes(rec.header.InfSize);
            rec.VariableData = br.ReadBytes(rec.header.DLC);

            return rec;
        }*/
    }
}
