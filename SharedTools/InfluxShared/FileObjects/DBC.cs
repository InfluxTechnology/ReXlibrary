using InfluxShared.Helpers;
using InfluxShared.Objects;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace InfluxShared.FileObjects
{
    public enum DBCByteOrder : byte { Intel, Motorola }
    public enum DBCMessageType : byte { Standard, Extended, CanFDStandard, CanFDExtended, J1939PG, Lin, KanCan, reserved }
    public enum DBCValueType : byte { Unsigned, Signed, IEEEFloat, IEEEDouble, ASCII, BYTES }
    public enum DBCSignalType : byte { Standard, Mode, ModeDependent }
    public enum DBCFileType : byte { None, Generic, CAN, CANFD, LIN, J1939, Ethernet, FlexRay, KanCan };

    public class DbcSelection
    {
        public bool Log { get; set; }
        public DbcItem Item { get; set; }
    }

    public class DbcItem : BasicItemInfo, ICanSignal
    {
        public ushort StartBit { get; set; }
        public ushort BitCount { get; set; }
        public DBCSignalType Type { get; set; }
        public string DisplayType => Type switch
        {
            DBCSignalType.Mode => "Mode",
            DBCSignalType.ModeDependent => "Mode dependent - 0x" + Mode.ToString("X"),
            _ => ""
        };
        public UInt64 Mode { get; set; }   //If the signal is Mode Dependent
        public DBCByteOrder ByteOrder { get; set; }
        public DBCValueType ValueType { get; set; }
        public bool Log { get; set; }
        public byte UDS { get; set; } = 0;

        public override string ToString() => Name;
        public double Factor => Conversion.Type.HasFlag(ConversionType.Formula) ? Conversion.Formula.CoeffB / Conversion.Formula.CoeffF : 1;
        public double Offset => Conversion.Type.HasFlag(ConversionType.Formula) ? Conversion.Formula.CoeffC / Conversion.Formula.CoeffF : 0;
        public TableNumericConversion Table => Conversion.Type.HasFlag(ConversionType.TableNumeric) ? Conversion.TableNumeric : null;

        internal DbcMessage Parent { get; set; }
        public override uint Ident => Parent?.CANID ?? 0;
        public override string IdentHex => Parent?.HexIdent ?? "";

        public bool EqualProps(object item) =>
            item is DbcItem dbc &&
            dbc.StartBit == StartBit &&
            dbc.BitCount == BitCount &&
            dbc.Type == Type &&
            dbc.Mode == Mode &&
            dbc.ByteOrder == ByteOrder &&
            dbc.ValueType == ValueType;

        public DbcItem Clone => (DbcItem)MemberwiseClone();

        public override ChannelDescriptor GetDescriptor => new ChannelDescriptor()
        {
            StartBit = StartBit,
            BitCount = BitCount,
            isIntel = ByteOrder == DBCByteOrder.Intel,
            HexType = BinaryData.BinaryTypes[(int)ValueType],
            conversionType = Conversion.Type,
            Factor = Factor,
            Offset = Offset,
            Table = Table,
            Name = Name,
            Units = Units
        };
    }

    public class DbcMessage : ICanMessage
    {
        public string Name { get; set; }
        public uint CANID { get; set; }
        public string HexIdent => "0x" + (isExtended ? CANID.ToString("X8") : CANID.ToString("X3"));
        public byte DLC { get; set; }
        public DBCMessageType MsgType { get; set; }
        public bool isExtended => MsgType == DBCMessageType.Extended || MsgType == DBCMessageType.CanFDExtended || MsgType == DBCMessageType.J1939PG;
        public string Transmitter { get; set; }
        public string Comment { get; set; }
        public List<ICanSignal> Signals => Items.Cast<ICanSignal>().ToList();
        public List<DbcItem> Items { get; set; }
        public bool Log { get; set; }

        public uint FullID => isExtended ? (uint)(CANID | (1 << 31)) : CANID;

        public bool EqualProps(object obj) =>
            obj is not null && obj is DbcMessage msg &&
            msg.MsgType == MsgType &&
            msg.CANID == CANID;

        public DbcMessage()
        {
            Items = new List<DbcItem>();
        }
    }

    public class DBC
    {
        public DBCFileType FileType { get; set; }
        public string FileName { get; set; } = "";
        public string FileNameSerialized { get; set; } = "";  //Imeto na DBC-to zapisano kato serialized file
        public string FileNameNoExt => Path.GetFileNameWithoutExtension(FileName);
        public string FileLocation => Path.GetDirectoryName(FileName);
        public List<string> NodeNames = ["Influx_Logger"];
        public List<DbcMessage> Messages { get; set; }

        public bool Equals(DBC dbc)
        {
            if (this != null && dbc != null)
            {
                if (this.FileNameSerialized == dbc.FileNameSerialized)
                    return true;
            }
            return false;
        }

        public DBC()
        {
            Messages = new List<DbcMessage>();
        }

        public static DBC CreateMode(List<PollingItem> ItemList, byte UDSService, byte ModeSize)
        {
            DBC dbc = new DBC();
            var groupedItems = ItemList.Where(uds => uds.UDSServiceID == UDSService).GroupBy(i => i.RxIdent).Select(grp => grp.ToList()).ToList();
            foreach (var items in groupedItems)
            {
                var msg = new DbcMessage()
                {
                    Name = "Mode 0x" + UDSService.ToString("X2"),
                    CANID = items[0].RxIdent,
                    DLC = 8,
                    MsgType = DBCMessageType.Standard
                };

                dbc.Messages.Add(msg);
                msg.Items.Add(new DbcItem()
                {
                    Name = "mode" + UDSService.ToString("X2"),
                    ItemType = 0,
                    StartBit = 0,
                    BitCount = (ushort)(ModeSize * 8),
                    ByteOrder = DBCByteOrder.Intel,
                    Type = DBCSignalType.Mode,
                    ValueType = DBCValueType.Unsigned,
                    UDS = UDSService,
                });

                foreach (var sig in items)
                {
                    DbcItem newSig = new DbcItem();
                    sig.CopyProperties(newSig);
                    newSig.Type = DBCSignalType.ModeDependent;
                    newSig.UDS = UDSService;
                    if (UDSService == 0x22)
                        newSig.Mode = 0x62 |
                            (UInt64)(sig.Ident & 0x00FF) << 16 |
                            (sig.Ident & 0xFF00);
                    else if (UDSService == 0x23)
                        newSig.Mode = 0x63 |
                            (UInt64)(sig.Ident & 0x000000FF) << 32 | (sig.Ident & 0x0000FF00) << 16 | 
                            (sig.Ident & 0x00FF0000) | (sig.Ident & 0xFF000000) >> 16;

                    newSig.Ident = sig.RxIdent;
                    newSig.StartBit += (UInt16)(msg.Items[0].StartBit + msg.Items[0].BitCount);
                    msg.Items.Add(newSig);
                    msg.DLC = (byte)Math.Max(msg.DLC, (newSig.StartBit + newSig.BitCount + 7) / 8);
                }
            }

            return dbc;
        }

        public static DBC CreateMode2A2C(List<PollingItem> ItemList)
        {
            DBC dbc = new DBC();
            var groupedItems = ItemList.Where(uds => uds.UDSServiceID == 0x2A).GroupBy(i => i.RxIdent).Select(grp => grp.ToList()).ToList();
            foreach (var items in groupedItems)
            {
                var msg = new DbcMessage()
                {
                    Name = "Mode 2A2C",
                    CANID = items[0].RxIdent,
                    DLC = 8,
                    MsgType = DBCMessageType.Standard
                };

                dbc.Messages.Add(msg);
                msg.Items.Add(new DbcItem()
                {
                    Name = "mode 2A2C",
                    ItemType = 0,
                    StartBit = 0,
                    BitCount = 8,
                    ByteOrder = DBCByteOrder.Intel,
                    Type = DBCSignalType.Mode,
                    ValueType = DBCValueType.Unsigned,
                    //UDS = 0x2A,
                });

                byte sigMode = 0;
                byte sigPos;
                byte sigEnd = 1;

                foreach (var sig in items)
                {
                    DbcItem newSig = new DbcItem();
                    sig.CopyProperties(newSig);
                    newSig.Type = DBCSignalType.ModeDependent;
                    newSig.UDS = 0;// 0x2A;
                    newSig.Ident = sig.RxIdent;

                    sigPos = sigEnd;
                    sigEnd = (byte)(sigPos + newSig.BitCount / 8);
                    if (sigEnd > 8)
                    {
                        sigPos = 1;
                        sigEnd = (byte)(sigPos + newSig.BitCount / 8);
                        sigMode++;
                    }

                    newSig.Mode = sigMode;
                    newSig.StartBit += (UInt16)(sigPos * 8);

                    msg.Items.Add(newSig);
                    msg.DLC = (byte)Math.Max(msg.DLC, (newSig.StartBit + newSig.BitCount + 7) / 8);
                }
            }

            return dbc;
        }

        public static DBC CreateXCP(List<PollingItem> ItemList, uint CRO)
        {
            DBC dbc = new DBC();

            dbc.Messages.Add(new DbcMessage()
            {
                Name = "CRO",
                CANID = CRO,
                DLC = 8,
                MsgType = DBCMessageType.Standard
            });

            var groupedItems = ItemList.Where(uds => uds.Service == ServiceType.XCP).GroupBy(i => i.UDSServiceID);
            foreach (var group in groupedItems)
            {
                var items = group.ToList();
                var msg = new DbcMessage()
                {
                    Name = "DTO ",
                    CANID = items[0].RxIdent,
                    DLC = 8,
                    MsgType = DBCMessageType.Standard
                };

                dbc.Messages.Add(msg);

                msg.Items.Add(new DbcItem()
                {
                    Name = "DTOPID",
                    ItemType = 0,
                    StartBit = 0,
                    BitCount = 8,
                    ByteOrder = DBCByteOrder.Intel,
                    Type = DBCSignalType.Mode,
                    ValueType = DBCValueType.Unsigned,
                });

                foreach (var sig in items)
                {
                    DbcItem newSig = new DbcItem();
                    sig.CopyProperties(newSig);
                    newSig.Type = DBCSignalType.ModeDependent;
                    newSig.UDS = 0;
                    newSig.Ident = sig.RxIdent;
                    //newSig.StartBit += 8;

                    msg.Items.Add(newSig);
                    msg.DLC = (byte)Math.Max(msg.DLC, (newSig.StartBit + newSig.BitCount + 7) / 8);
                }
            }

            return dbc;
        }

        public void AddToReferenceCollection(ReferenceCollection collection, byte BusChannel)
        {
            foreach (var msg in Messages)
                foreach (var sig in msg.Items)
                    collection.Add(new ReferenceDbcChannel()
                    {
                        BusChannelIndex = BusChannel,
                        FileName = FileNameSerialized,
                        MessageID = msg.FullID,
                        SignalName = sig.Name
                    });
        }
    }

    public class ExportDbcMessage
    {
        public class Multiplex
        {
            public ICanSignal multiplexor = null;
            public BinaryData multiplexorData = null;
            // Multiplexor map is dictionary with pair - mode value and list of signal indexes
            public Dictionary<UInt64, List<UInt16>> multiplexorMap = null;
            // Mode dependant group ids
            public List<UInt64> multiplexorGroups = null;

            public List<ICanSignal> Signals;
        }

        public UInt64 uniqueid { get; set; }
        public byte BusChannel { get; set; }
        public DbcMessage Message { get; set; }
        public List<ICanSignal> Signals { get; set; }

        public Dictionary<byte, Multiplex> MultiplexDict = new() { { 0, new() } };

        public static bool operator ==(ExportDbcMessage item1, ExportDbcMessage item2) => item1.BusChannel == item2.BusChannel && item1.Message.EqualProps(item2.Message);
        public static bool operator !=(ExportDbcMessage item1, ExportDbcMessage item2) => !(item1 == item2);
        public override bool Equals(object obj)
        {
            if (obj is ExportDbcMessage)
                return this == (ExportDbcMessage)obj;
            else
                return false;
        }

        public void AddSignal(ICanSignal Signal)
        {
            Signals.Add(Signal);
            Multiplex mp;
            if (!MultiplexDict.TryGetValue(Signal.UDS, out mp))
                MultiplexDict.Add(Signal.UDS, mp = new());

            mp.multiplexor = GetMode(Signal.UDS);
            if (mp.multiplexor is not null)
                mp.multiplexorData = mp.multiplexor.GetDescriptor.CreateBinaryData();
        }

        public ICanSignal GetMode(byte uds = 0)
        {
            Multiplex mp = MultiplexDict[uds];
            mp.Signals = Signals.Where(s => s.UDS == uds).ToList();

            if (mp.Signals[0].Type == DBCSignalType.Mode)
            {
                mp.multiplexorMap =
                    mp.Signals.Where(sg => sg.Type == DBCSignalType.ModeDependent).
                    GroupBy(md => md.Mode,
                    (k, c) => new { ModeValue = (UInt64)k, Indexes = c.Select(cs => (UInt16)mp.Signals.IndexOf(cs)).ToList() }).
                    ToDictionary(d => d.ModeValue, d => d.Indexes);

                return mp.Signals[0];
            }
            else
            {
                ICanSignal mode = mp.Signals.FirstOrDefault(s => s.Type == DBCSignalType.Mode);
                if (mode is not null)
                {
                    mp.Signals.Remove(mode);
                    mp.Signals.Insert(0, mode);

                    mp.multiplexorMap =
                        mp.Signals.Where(sg => sg.Type == DBCSignalType.ModeDependent).
                        GroupBy(md => md.Mode,
                        (k, c) => new { ModeValue = (UInt64)k, Indexes = c.Select(cs => (UInt16)mp.Signals.IndexOf(cs)).ToList() }).
                        ToDictionary(d => d.ModeValue, d => d.Indexes);
                }
                return mode;
            }
        }

        public UInt16 FindMultiplexorIndex(DbcItem signal)
        {
            Multiplex mp = MultiplexDict[signal.UDS];
            var keys = mp.multiplexorMap.Where(pair => pair.Value.Contains((ushort)mp.Signals.IndexOf(signal))).
                Select(pair => pair.Key);

            if (keys.Count() > 0)
                return (UInt16)mp.multiplexorMap.Keys.ToList().IndexOf(keys.First());
            else
                return UInt16.MaxValue;
        }
    }

    public class ExportDbcCollection : List<ExportDbcMessage>
    {
        public ExportDbcMessage AddMessage(byte BusChannel, DbcMessage Message)
        {
            foreach (ExportDbcMessage m in this)
                if (m.BusChannel == BusChannel && m.Message.EqualProps(Message))
                    return m;

            ExportDbcMessage channel = new ExportDbcMessage()
            {
                BusChannel = BusChannel,
                Message = Message,
                Signals = new List<ICanSignal>()
            };
            Add(channel);
            return channel;
        }

    }

    public static class DbcHelper
    {
        public static string ToDisplayName(this DBCMessageType msgType)
        {
            switch (msgType)
            {
                case DBCMessageType.Standard: return "CAN Standard";
                case DBCMessageType.Extended: return "CAN Extended";
                case DBCMessageType.CanFDStandard: return "CAN FD Standard";
                case DBCMessageType.CanFDExtended: return "CAN FD Extended";
                case DBCMessageType.J1939PG: return "J1939 PG (ext. ID)";
                case DBCMessageType.Lin: return "Lin";
                case DBCMessageType.KanCan: return "KanCan";
                default: return "Unknown";
            }
        }
    }
}
