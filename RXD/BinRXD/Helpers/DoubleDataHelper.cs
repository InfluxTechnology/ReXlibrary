using InfluxShared.FileObjects;
using RXD.Blocks;
using System;

namespace RXD.Helpers
{
    internal static class DoubleDataHelper
    {
        public static DoubleData Object(this DoubleDataCollection ddata, BinBase bin)
        {
            DoubleData data = ddata.GetObject(bin.header.uniqueid);
            if (data is null)
            {
                ProcessingRulesCollection prc = null;
                if (bin.BinType == BlockType.Trigger)
                {
                    prc = ddata.ProcessingRules;
                    ddata.ProcessingRules = null;
                }
                data = ddata.Add(bin.header.uniqueid, ChannelName: bin.GetName, ChannelUnits: bin.GetUnits);
                if (bin.BinType == BlockType.Trigger)
                {
                    ddata.ProcessingRules = prc;
                }
                data.BinaryHelper = bin.GetDataDescriptor.CreateBinaryData();
                // if (bin.BinType == BlockType.CANInterface)
                //     data.BusChannel = (bin as BinCanInterface)[BinCanInterface.BinProp.PhysicalNumber];
            }

            return data;
        }

        public static DoubleData Object(this DoubleDataCollection ddata, BasicItemInfo signal, UInt64 id, byte SourceAddress = 0xFF)
        {
            id |= (UInt64)SourceAddress << 33;
            if (signal.TempObj is not null)
                return signal.TempObj as DoubleData;

            signal.TempObj = ddata.GetObject(id);
            if (signal.TempObj is null)
            {
                ChannelDescriptor ChannelDesc = signal.GetDescriptor;
                signal.TempObj = ddata.Add(id, ChannelName: ChannelDesc.Name, ChannelUnits: ChannelDesc.Units);
                (signal.TempObj as DoubleData).BinaryHelper = ChannelDesc.CreateBinaryData();
                //if (bin.BinType == BlockType.CANInterface)
                //    data.BusChannel = (bin as BinCanInterface)[BinCanInterface.BinProp.PhysicalNumber];

                if (SourceAddress != 0xFF)
                    (signal.TempObj as DoubleData).ChannelName += " [SA " + SourceAddress.ToString("X2") + "]";
            }
            return signal.TempObj as DoubleData;
        }
    }
}
