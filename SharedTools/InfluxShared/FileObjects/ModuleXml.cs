using A2lParserLib;
using A2lParserLib.Interfaces;
using A2lParserLib.Settings;
using InfluxShared.Generic;
using InfluxShared.Helpers;
using InfluxShared.Objects;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Xml;
using System.Xml.Schema;
using System.Xml.Serialization;

namespace InfluxShared.FileObjects
{
    public enum ServiceType : byte
    {
        UDS,
        XCP
    }

    [Serializable]
    [XmlRoot("REXMODULE", Namespace = "http://www.influxtechnology.com/xml/ReXModule")]
    public class ModuleXml
    {
        [XmlElement("CONFIG")]
        public Config Config { get; set; }

        [XmlElement("CONFIG_ITEM_LIST")]
        public ConfigItemList ConfigItemList { get; set; }

        [XmlElement("CONFIG_XCP")]
        public XcpConfigXml XcpCfg { get; set; }

        [XmlElement("PERIODIC_ITEM_LIST")]
        public PeriodicItemList PeriodicItemList { get; set; }

        [XmlElement("POLLING_ITEM_LIST")]
        public PollingItemList PollingItemList { get; set; }

        public ModuleXml()
        {
            PollingItemList = new();
            PeriodicItemList = new();
            ConfigItemList = new ConfigItemList();
            Config = new Config();
            XcpCfg = new();
            //XcpCfg.DaqList = new();            
        }

        public static ModuleXml ReadFile(string xmlPath)
        {
            // Deserialize the XML file into RexModule object
            XmlSerializer serializer = new XmlSerializer(typeof(ModuleXml));
            using (FileStream stream = new FileStream(xmlPath, FileMode.Open))
            {
                ModuleXml xml = ReadStream(stream);
                return xml;
            }
        }
        public static ModuleXml ReadStream(Stream stream)
        {
            // Deserialize the XML file into RexModule object
            XmlSerializer serializer = new XmlSerializer(typeof(ModuleXml));
            ModuleXml xml = (ModuleXml)serializer.Deserialize(stream);
            
            return xml;

        }

        public static bool ValidateXml(string xmlPath, string schemaPath, out string error)
        {
            XmlReaderSettings settings = new XmlReaderSettings();
            settings.ValidationType = ValidationType.Schema;

            settings.Schemas.Add(null, schemaPath);
            using (XmlReader reader = XmlReader.Create(xmlPath, settings))
            {
                try
                {
                    while (reader.Read()) { }
                    error = "XML file is valid against the schema.";
                    return true;
                }
                catch (XmlException ex)
                {
                    error = $"XML exception: {ex.Message}";
                    return false;
                }
                catch (XmlSchemaValidationException ex)
                {
                    error = $"Schema validation exception: {ex.Message}";
                    return false;
                }
            }
        }
    }

    public static class ModuleXmlHelper
    {
        public static void ToFile(this ModuleXml moduleXml, string fileName)
        {
            XmlSerializer serializer = new XmlSerializer(typeof(ModuleXml));
            using (FileStream stream = new FileStream(fileName, FileMode.Create))
            {
                serializer.Serialize(stream, moduleXml);
            }
        }
        public static Stream ToStream(this ModuleXml moduleXml)
        {
            XmlSerializer serializer = new XmlSerializer(typeof(ModuleXml));
            Stream stream = new MemoryStream();
            serializer.Serialize(stream, moduleXml);
            stream.Position = 0;
            return stream;
        }
    }

    public class PollingItemList
    {
        public PollingItemList() { Items = new List<PollingItem>(); }
        [XmlElement("POLLING_ITEM")]
        public List<PollingItem> Items { get; set; }
    }

    public class PeriodicItemList
    {
        public PeriodicItemList() { Items = new List<PeriodicItem>(); }
        [XmlElement("PERIODIC_ITEM")]
        public List<PeriodicItem> Items { get; set; }
    }

    public class XcpDaqListXml //Used to serialize DAQ to DAQ_LIST
    {
        public XcpDaqListXml() { Daqs = new List<XcpDaqXml>(); }
        [XmlElement("DAQ")]
        public List<XcpDaqXml> Daqs { get; set; }
    }

    public class XcpEventListXml //Used to serialize Events to EVENT_LIST
    {
        public XcpEventListXml() { Events = new List<XcpEventXml>(); }
        [XmlElement("EVENT")]
        public List<XcpEventXml> Events { get; set; }
    }

    public class XcpCommands //Used to serialize Commands to COMMANDS_LIST
    {
        public XcpCommands() { Cmmds = new List<string>(); }
        [XmlElement("COMMAND")]
        public List<string> Cmmds { get; set; }
    }

    public class Config
    {
        [XmlElement("CAN_BUS")]
        public byte CanBus { get; set; }
        [XmlElement("GUID")]
        public string Guid { get; set; }
        [XmlElement("NAME")]
        public string Name { get; set; }
        [XmlElement("VERSION")]
        public byte Version { get; set; }
        [XmlElement("ADDRESS_SIZE")]
        public byte AddressSize { get; set; } = 4;
        [XmlElement("DATA_SIZE")]
        public byte DataSize { get; set; } = 1;
        [XmlElement("DYNAMIC_IDENT_START")]
        public uint DynIdentStart { get; set; } = 0xF200;
        [XmlElement("DYNAMIC_SIGNAL")]
        public uint DynamicSignal { get; set; } = 0x6A0;
    }

    public class XcpConfigXml : IXcpSettings
    {
        XcpDaqListXml _XcpDaqListXml = new();
        List<XcpDaq> _XcpDaqList = new();
        XcpEventListXml _XcpEventListXml = new();
        List<XcpEvent> _XcpEventList = new();

        [XmlElement("CRO")]
        public uint Cro { get; set; }

        [XmlElement("DTO")]
        public uint Dto { get; set; }
        [XmlElement("NAME")]
        public string Name { get; set; }
        [XmlElement("STATION_ADDRESS")]
        public ushort StationAddress { get; set; }
        [XmlElement("MAX_DAQ")]
        public uint MaxDaq { get; set; }
        [XmlElement("MAX_EVENTS")]
        public uint MaxEventChannels { get; set; }
        [XmlElement("MIN_DAQ")]
        public byte MinDaq { get; set; }
        [XmlElement("DAQ_TYPE")]
        public Enums.DaqType DaqType { get; set; }
        [XmlElement("BYTE_ORDER")]
        public Enums.ByteOrder ByteOrder { get; set; }
        [XmlElement("BAUDRATE")]
        public uint Baudrate { get; set; }
        [XmlElement("BAUDRATE_FD")]
        public uint BaudrateFD { get; set; }
        [XmlIgnore]
        public byte RateIndex { get; set; }
        [XmlElement("ODT_SIZE")]
        public ushort OdtSize { get; set; }
        [XmlElement("ODT_ENTRY_SIZE")]
        public ushort OdtEntrySize { get; set; }
        [XmlIgnore]
        public List<XcpDaq> Daqs { get => _XcpDaqList; set => SetDaqList(value); }      
        [XmlElement("DAQ_LIST")]
        public XcpDaqListXml DaqsXml { get=> _XcpDaqListXml; set => SetDaqListXml(value); }         
        [XmlIgnore]
        public List<XcpEvent> Events { get=> _XcpEventList; set => SetEventList(value); } 
        [XmlElement("EVENT_LIST")]
        public XcpEventListXml EventsXml { get => _XcpEventListXml; set => SetEventListXml(value); } 
        [XmlIgnore]
        public List<string> Cmmds { get; set; } = new(); 
        [XmlElement("COMMANDS")]
        public XcpCommands CmmdsXml { get; set; } = new();

        private void SetDaqListXml(XcpDaqListXml value)
        {
            _XcpDaqListXml = value;
            Daqs.Clear();
            foreach (var daqXml in _XcpDaqListXml.Daqs)
            {
                XcpDaq daq = new XcpDaq();
                daqXml.CopyProperties(daq);
                Daqs.Add(daq);
            }
        }

        private void SetDaqList(List<XcpDaq> value)
        {
            _XcpDaqList = value;
            DaqsXml.Daqs.Clear();
            foreach (var daq in _XcpDaqList)
            {
                XcpDaqXml daqXml = new XcpDaqXml();
                daq.CopyProperties(daqXml);
                DaqsXml.Daqs.Add(daqXml);
            }
        }

        private void SetEventListXml(XcpEventListXml value)
        {
            _XcpEventListXml = value;
            Events.Clear();
            foreach (var eventXml in _XcpEventListXml.Events)
            {
                XcpEvent eventA2l = new ();
                eventXml.CopyProperties(eventA2l);
                Events.Add(eventA2l);
            }
        }

        private void SetEventList(List<XcpEvent> value)
        {
            _XcpEventList = value;
            EventsXml.Events.Clear();
            foreach (var eventA2l in _XcpEventList)
            {
                XcpEventXml eventXml = new ();
                eventA2l.CopyProperties(eventXml);
                EventsXml.Events.Add(eventXml);
            }
        }
    }

    public class XcpEventXml: IXcpEvent
    {
        [XmlElement("NAME")]
        public string Name { get; set; }
        [XmlElement("SHORT_NAME")]
        public string ShortName { get; set; }
        [XmlElement("CHANNEL_INDEX")]
        public byte Channel { get; set; }
        [XmlElement("MAX_DAQ_LIST")]
        public int MaxDaqList { get; set; } //Maximum number of DAQ lists in this event channel
        [XmlElement("TIMECYCLE")]
        public byte TimeCycle { get; set; } //Event channel time cycle
        [XmlElement("TIMEUNIT")]
        public byte TimeUnit { get; set; } //Event channel time unit
    }

    public class XcpDaqXml : IXcpDaq
    {
        [XmlElement("NAME")]
        public string Name { get; set; } = "";
        [XmlElement("DAQ_INDEX")]
        public byte DaqIndex { get; set; }
        [XmlElement("IDENT")]
        public uint Ident { get; set; }
        [XmlElement("PRESCALE")]
        public ulong Sampling { get; set; } 
        [XmlElement("PRESCALE_STRING")]
        public string SamplingStr { get; set; } = "";
        [XmlElement("MAX_ODT")]
        public byte MaxOdt { get; set; }
        [XmlElement("FIRST_PID")]
        public short FirstPid { get; set; }
        [XmlElement("MAX_ODT_ENTRIES")]
        public byte MaxOdtEntries { get; set; }
        [XmlElement("EVENT_CHANNEL")]
        public byte EventChannel { get; set; }
        [XmlElement("EVENT_FIXED")]
        public bool EventFixed { get; set; }
        [XmlIgnore]
        public List<XcpOdt> Odts { get; set; } = new();
    }

    public class ConfigItemList
    {
        public ConfigItemList() { Items = new List<ConfigItem>(); }
        [XmlElement("CONFIG_ITEM")]
        public List<ConfigItem> Items { get; set; }
    }

    public class ConfigItem
    {

        [XmlIgnore]
        public byte[] Data { get; set; }
        [XmlElement("DATA")]
        public string DataHex
        {
            get => Bytes.ToHexBinary(Data);
            set => Data = Bytes.FromHexBinary(value);
        }

        [XmlElement("DELAY")]
        public long Delay { get; set; }
        [XmlElement("ORDER")]
        public byte Order { get; set; }
        [XmlElement("TX_IDENT")]
        public uint TxIdent { get; set; }
        [XmlElement("RX_IDENT")]
        public uint RxIdent { get; set; }
        
    }

    public class Item : ICanSignal
    {
        [XmlElement("BIT_COUNT")]
        public ushort BitCount { get; set; }

        [XmlElement("DATA_TYPE")]
        public string DataType
        {
            get => ValueType.ToString().ToUpper();
            set
            {
                if (Enum.TryParse(value, true, out DBCValueType VT))
                    ValueType = VT;
            }
        }

        [XmlElement("ENDIAN")]
        public string Endian
        {
            get => ByteOrder.ToString().ToUpper();
            set
            {
                if (Enum.TryParse(value, true, out DBCByteOrder BO))
                    ByteOrder = BO;
            }
        }

        [XmlElement("FACTOR")]
        public double Factor 
        {
            get => Conversion.Type.HasFlag(ConversionType.Formula) ? Conversion.Formula.CoeffB : 1;
            set
            {
                Conversion.Type = ConversionType.Formula;
                Conversion.Formula.CoeffB = value;
            }
        }

        [XmlElement("MAXIMUM")]
        public double MaxValue { get; set; }

        [XmlElement("MINIMUM")]
        public double MinValue { get; set; }

        [XmlElement("NAME")]
        public string Name { get; set; }

        [XmlElement("OFFSET")]
        public double Offset 
        {
            get => Conversion.Type.HasFlag(ConversionType.Formula) ? Conversion.Formula.CoeffC : 1;
            set
            {
                Conversion.Type = ConversionType.Formula;
                Conversion.Formula.CoeffC = value;
            }
        }

        [XmlElement("SERVICE_IDENT")]
        public uint Ident { get; set; }

        [XmlElement("START_BIT")]
        public ushort StartBit { get; set; }

        [XmlElement("UNITS")]
        public string Units { get; set; }

        public string IdentHex => "0x" + Ident.ToString("X4");

        [XmlIgnore]
        public byte ItemType { get; set; }
        [XmlIgnore]
        public string Comment { get; set; }
        [XmlIgnore]
        public DBCSignalType Type { get; set; }
        [XmlIgnore]
        public DBCByteOrder ByteOrder { get; set; }

        [XmlElement("MODE")]
        public UInt64 Mode { get; set; }
        [XmlIgnore]
        public DBCValueType ValueType { get; set; }

        [XmlIgnore]
        public ItemConversion Conversion { get; set; } = new();

        public ChannelDescriptor GetDescriptor => new ChannelDescriptor()
        {
            StartBit = StartBit,
            BitCount = BitCount,
            isIntel = ByteOrder == DBCByteOrder.Intel,
            HexType = BinaryData.BinaryTypes[(int)ValueType],
            conversionType = Conversion.Type,
            Factor = Factor,
            Offset = Offset,
            Table = null,
            Name = Name,
            Units = Units
        };
        [XmlIgnore]
        public bool Log { get; set; }
        [XmlIgnore]
        public byte UDS { get; set; }
        [XmlIgnore]
        public object TagObject { get; set; }

        public bool EqualProps(object item)
        {
            throw new NotImplementedException();
        }
    }

    public class PeriodicItem : Item
    {
        [XmlElement("MODE_BIT_COUNT")]
        public byte ModeBitCount { get; set; }
        [XmlElement("MODE_START_BIT")]
        public short ModeStartBit { get; set; }
        [XmlElement("MODE_VALUE")]
        public int ModeValue { get; set; }
    }

    public class PollingItem : Item
    {
        [XmlElement("DELAY")]
        public long Delay { get; set; }

        [XmlElement("ORDER")]
        public byte Order { get; set; }

        [XmlElement("SERVICE")]
        public ServiceType Service { get; set; } = ServiceType.UDS;

        [XmlElement("TX_IDENT")]
        public uint TxIdent { get; set; }

        [XmlElement("RX_IDENT")]
        public uint RxIdent { get; set; }

        [XmlElement("UDS_SERVICE_ID")]
        public byte UDSServiceID { get => UDS; set => UDS = value; }  //If service is Xcp used for Daq index
        [XmlIgnore]
        public string SamplingStr { get; set; }
        public PollingItem() { }

        public PollingItem(ICanSignal msg) => msg.CopyProperties(this);
    }

    


}
