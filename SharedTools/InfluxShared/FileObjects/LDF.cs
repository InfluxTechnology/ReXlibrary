using System;
using System.Collections.Generic;
using System.IO;

namespace InfluxShared.FileObjects
{
    public enum LDFMessageType : byte { Lin }

    public class LdfItem : BasicItemInfo
    {
        public ushort StartBit { get; set; }
        public ushort BitCount { get; set; }
        public string SourceNode { get; set; }
        public string ReceiverNodes { get; set; }
        public DBCByteOrder ByteOrder => DBCByteOrder.Intel;
        public DBCValueType ValueType => DBCValueType.Unsigned;
        public bool Log { get; set; }
        public override string ToString()
        {
            return Name;
        }
        public double Factor => Conversion.Formula.CoeffB;
        public double Offset => Conversion.Formula.CoeffC;

        public LdfItem Clone => (LdfItem)MemberwiseClone();

        public static bool operator ==(LdfItem item1, LdfItem item2) =>
            item1.StartBit == item2.StartBit &&
            item1.BitCount == item2.BitCount;
        public static bool operator !=(LdfItem item1, LdfItem item2) => !(item1 == item2);
        public override bool Equals(object obj)
        {
            if (obj is LdfItem)
                return this == (LdfItem)obj;
            else
                return false;
        }

        public override ChannelDescriptor GetDescriptor => new ChannelDescriptor()
        {
            StartBit = StartBit,
            BitCount = BitCount,
            isIntel = true,
            HexType = typeof(UInt64),
            conversionType = ConversionType.Formula,
            Factor = Factor,
            Offset = Offset,
            Name = Name,
            Units = Units
        };

    }

    public class LdfSlot
    {
        public string Frame { get; set; }
        public ushort Delay { get; set; }
        public ushort FrameID { get; set; }
        public byte DLC { get; set; }
    }

    public class LdfTable
    {
        public string Name { get; set; }
        public ushort SlotCount { get => (ushort)Slots.Count; }
        public List<LdfSlot> Slots { get; set; }
        public LdfTable()
        {
            Slots = new List<LdfSlot>();
        }
    }

    public class LdfMessage
    {
        public string Name { get; set; }
        public uint ID { get; set; }
        public string HexIdent => "0x" + ID.ToString("X2");
        public byte DLC { get; set; }
        public LDFMessageType MsgType => LDFMessageType.Lin;
        public string Publisher { get; set; }
        public string Comment { get; set; }
        public List<LdfItem> Items { get; set; }
        public bool Log { get; set; }

        public static bool operator ==(LdfMessage item1, LdfMessage item2) => !(item1 is null) && !(item2 is null) && item1.ID == item2.ID;
        public static bool operator !=(LdfMessage item1, LdfMessage item2) => !(item1 == item2);
        public override bool Equals(object obj)
        {
            if (obj is LdfMessage)
                return this == (LdfMessage)obj;
            else
                return false;
        }

        public LdfMessage()
        {
            Items = new List<LdfItem>();
        }


    }

    public class LDF
    {
        public string FileName { get; set; }
        public string FileNameSerialized { get; set; }  //Imeto na LDF-a zapisano kato serialized file
        public string FileNameNoExt => Path.GetFileNameWithoutExtension(FileName);
        public string FileLocation => Path.GetDirectoryName(FileName);
        public List<LdfMessage> Messages { get; set; }
        public ushort TableCount { get => (ushort)Tables.Count; }
        public List<LdfTable> Tables { get; set; }

        public bool Equals(LDF ldf)
        {
            if (this != null && ldf != null)
            {
                if (this.FileNameSerialized == ldf.FileNameSerialized)
                    return true;
            }
            return false;
        }

        public LDF()
        {
            Messages = new List<LdfMessage>();
            Tables = new List<LdfTable>();
        }

        public void AddToReferenceCollection(ReferenceCollection collection, byte BusChannel)
        {
            foreach (var msg in Messages)
                foreach (var sig in msg.Items)
                    collection.Add(new ReferenceLdfChannel()
                    {
                        BusChannelIndex = BusChannel,
                        FileName = FileNameSerialized,
                        MessageID = (byte)msg.ID,
                        SignalName = sig.Name
                    });
        }
    }

    public class ExportLdfMessage
    {
        public UInt64 uniqueid { get; set; }
        public byte BusChannel { get; set; }
        public LdfMessage Message { get; set; }
        public List<LdfItem> Signals { get; set; }

        public static bool operator ==(ExportLdfMessage item1, ExportLdfMessage item2) => item1.BusChannel == item2.BusChannel && item1.Message == item2.Message;
        public static bool operator !=(ExportLdfMessage item1, ExportLdfMessage item2) => !(item1 == item2);
        public override bool Equals(object obj)
        {
            if (obj is ExportLdfMessage)
                return this == (ExportLdfMessage)obj;
            else
                return false;
        }

        public void AddSignal(LdfItem Signal)
        {
            Signals.Add(Signal.Clone);
        }
    }

    public class ExportLdfCollection : List<ExportLdfMessage>
    {
        public ExportLdfMessage AddMessage(byte BusChannel, LdfMessage Message)
        {
            foreach (ExportLdfMessage m in this)
                if (m.BusChannel == BusChannel && m.Message == Message)
                    return m;

            ExportLdfMessage channel = new ExportLdfMessage()
            {
                BusChannel = BusChannel,
                Message = Message,
                Signals = new List<LdfItem>()
            };
            Add(channel);
            return channel;
        }
    }

}
