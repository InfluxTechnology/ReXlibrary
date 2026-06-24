using InfluxShared.Generic;
using InfluxShared.Helpers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace InfluxShared.FileObjects
{

    public class OutputMessage
    {
        public string Name { get; set; } = string.Empty;

        public UInt32 CanID { get; set; } = 1;

        public DBCMessageType CanMsgType { get; set; }

        public string CanType
        {
            get => CanMsgType.ToDisplayName();
            set
            {
                for (DBCMessageType mt = DBCMessageType.Standard; mt <= DBCMessageType.CanFDExtended; mt++)
                    if (mt.ToDisplayName().ToLower().Replace(" ", "").Equals(value.ToLower().Replace(" ", "")))
                    {
                        CanMsgType = mt;
                        break;
                    }
            }
        }

        public bool BRS { get; set; }

        public CanFDMessageType CanFDOption =>
            (CanMsgType == DBCMessageType.Standard || CanMsgType == DBCMessageType.Extended)
            ? CanFDMessageType.NORMAL_CAN
            : BRS ? CanFDMessageType.FD_FAST_CAN : CanFDMessageType.FD_CAN;

        public byte DLC { get; set; }

        public string strDLC
        {
            get => DLC.ToString();
            set
            {
                if (int.TryParse(value, out int dlc))
                {
                    DLC = (byte)dlc;
                    byte[] data = Data;
                    Array.Resize(ref data, dlc);
                    Data = data;
                }
            }
        }

        public byte[] Data { get; set; }

        public string strData
        {
            get => ArrayToString(Data);
            set
            {
                string datastr = value.Replace(" ", "").Replace(Environment.NewLine, "").PadRight(DLC * 2, '0').Substring(0, DLC * 2);
                Data = Bytes.FromHexBinary(datastr);
            }
        }

        public bool Can0 { get; set; }
        public bool Can1 { get; set; }
        public bool Can2 { get; set; }
        public bool Can3 { get; set; }

        public static string ArrayToString(object obj)
        {
            string data = "";
            byte[] tmp = Bytes.ArrayToBytes(obj, (obj as Array).Length);
            if (tmp is not null)
                for (int i = 0; i < tmp.Length; i += 8)
                    data += BitConverter.ToString(tmp.Slice(i, 8)).Replace("-", " ") + Environment.NewLine;
            return data.Trim();
        }

        public UInt32 Period { get; set; }
        public UInt32 Delay { get; set; }
        public UInt16 UID { get; set; }
        public UInt16 NextOutputID { get; set; }
        public bool Linked { get; set; }
        public bool IsChild { get; set; }
        public bool LogTx { get; set; }
        public uint RxIdent {  get; set; }
        public uint Timeout { get; set; }
        public ushort Attempts { get; set; }
        public Protocol_Type ProtocolType { get; set; }
        public List<Object> LinkedParameters { get; set; } = new();

        public OutputMessage()
        {
            Period = 100;
            DLC = 8;
            Data = new byte[DLC];
        }
    }

    public static class OutputMessageListHelper
    {
        private const string XcpShortUploadChannelName = "Polling (Short upload)";

        public static bool LoadFromCsv(this List<OutputMessage> messages, string csvFile)
        {
            OutputMessage canMsg;
            OutputMessage lastCanMsg = null;
            int rowCounter = 1;
            try
            {
                using (StreamReader reader = new StreamReader(csvFile))
                {
                    messages.Clear();
                    string row = reader.ReadLine();
                    rowCounter++;
                    while (row != "")
                    {
                        row = reader.ReadLine();
                        if (row == null)
                            break;
                        string[] items = row.Split(',');
                        if (items.Length >= 10)
                        {
                            uint canId = (uint)Integers.StrToIntDef(items[0], 0);
                            // canMsg = bus.CanMessages.Where(x => x.CanID == canId).FirstOrDefault();
                            // if (canMsg == null)
                            // {
                            canMsg = new OutputMessage();
                            messages.Add(canMsg);
                            //  }
                            canMsg.CanID = canId;
                            canMsg.Linked = items[1] != "";
                            if (canMsg.Linked)
                            {
                                canMsg.IsChild = true;
                                if (!lastCanMsg.Linked)
                                    lastCanMsg.Linked = true;
                            }
                            canMsg.Period = (uint)Integers.StrToIntDef(items[2], 100);
                            canMsg.Delay = (uint)Integers.StrToIntDef(items[3], 0);
                            canMsg.Can0 = items[4] != "";
                            canMsg.Can1 = items[5] != "";
                            canMsg.Can2 = items[6] != "";
                            canMsg.Can3 = items[7] != "";
                            if (!canMsg.Can1 && !canMsg.Can2 && !canMsg.Can3)
                                canMsg.Can0 = true;
                            canMsg.CanMsgType = (DBCMessageType)Integers.StrToIntDef(items[8], 0); ;
                            canMsg.BRS = items[9] != "";
                            canMsg.DLC = (byte)Integers.StrToIntDef(items[10], 8); ;
                            string[] data = items[11].Split(' ');
                            canMsg.Data = new byte[canMsg.DLC];
                            for (int i = 0; i < data.Length; i++)
                            {
                                canMsg.Data[i] = (byte)Integers.StrToIntDef("0x" + data[i], 0);
                            }
                            lastCanMsg = canMsg;
                        }
                    }
                }
                return true;
            }
            catch (Exception exc)
            {
                //LastError = $"Error parsing csv row {rowCounter} Error: {exc.Message}";
                return false;
            }
        }

        public static bool LoadCfgMessagesFromModuleXml(this List<OutputMessage> messages, ModuleXml modXml)
        {
            foreach (var cfgMsg in modXml.ConfigItemList.Items.OrderBy(x => x.Order))
            {
                OutputMessage canMsg = new OutputMessage();
                messages.Add(canMsg);
                canMsg.CanID = cfgMsg.TxIdent;
                canMsg.RxIdent = cfgMsg.RxIdent;
                if (canMsg.CanID > 0x7FF)
                    canMsg.CanMsgType = DBCMessageType.Extended;
                canMsg.Data = cfgMsg.Data;
                canMsg.DLC = 8;// (byte)cfgMsg.Data.Length;
                canMsg.Delay = (uint)cfgMsg.Delay;
                canMsg.Timeout = 1000;
                canMsg.Attempts = 5;
            }
            return true;
        }

        public static bool LoadFromModuleXml(this List<OutputMessage> messages, ModuleXml modXml)
        {
            OutputMessage canMsg;
            try
            {
                string groupID = "";
                ushort dynSize = 0;
                uint dynIdentStart = modXml.Config.DynIdentStart;

                //messages.Clear();
                foreach (var xmlMsg in modXml.PollingItemList.Items.Where(item=>item.UDSServiceID ==0x23 || item.UDSServiceID == 0x22)
                                .GroupBy(x => x.Ident).Select(group => group.First()).OrderBy(x => x.Order))
                {
                    canMsg = new OutputMessage();
                    messages.Add(canMsg);
                    canMsg.Delay = (uint)xmlMsg.Delay;
                    canMsg.CanID = xmlMsg.TxIdent;
                    if (canMsg.CanID > 0x7FF)
                        canMsg.CanMsgType = DBCMessageType.Extended;
                    canMsg.IsChild = true;
                    if (groupID != $"CAN{modXml.Config?.CanBus}_{xmlMsg.TxIdent}")
                    {
                        canMsg.IsChild = false;
                        groupID = $"CAN{modXml.Config?.CanBus}_{xmlMsg.TxIdent}";
                    }
                    if (modXml.Config?.CanBus == 1)
                        canMsg.Can1 = true;
                    else if (modXml.Config?.CanBus == 2)
                        canMsg.Can2 = true;
                    else if (modXml.Config?.CanBus == 3)
                        canMsg.Can3 = true;
                    else
                        canMsg.Can0 = true;
                    if (xmlMsg.UDSServiceID == 0x23)
                    {
                        AddMode23Msg(modXml, canMsg, xmlMsg, messages);
                    }
                    else if (xmlMsg.UDSServiceID == 0x22)
                        canMsg.Data = new byte[8] { 3, 0x22, (byte)(xmlMsg.Ident >> 8), (byte)xmlMsg.Ident, 0, 0, 0, 0 };
                    canMsg.DLC = 8;// (byte)(xmlMsg.Data.Length);
                    canMsg.BRS = false;
                    canMsg.Linked = true;
                    canMsg.RxIdent = xmlMsg.RxIdent;
                    canMsg.ProtocolType = Protocol_Type.UDS;
                    canMsg.Timeout = 1000;
                }

                AddXcpShortUploadRepeatMessages(messages, modXml);
                AddCcpUploadRepeatMessages(messages, modXml);

                return true;
            }
            catch (Exception exc)
            {
                //LastError = $"Error parsing csv row {rowCounter} Error: {exc.Message}";
                return false;
            }
        }

        private static void AddMode23Msg(ModuleXml modXml, OutputMessage canMsg, PollingItem xmlMsg, List<OutputMessage> messages)
        {
            ushort size = (ushort)(xmlMsg.BitCount / 8);
            byte addrDataSize = Convert.ToByte($"{modXml.Config.DataSize}{modXml.Config.AddressSize}", 16);
            byte msgSize = (byte)(3 + modXml.Config.DataSize + modXml.Config.AddressSize);
            if (msgSize < 8)
                msgSize = 8;
            canMsg.Data = new byte[msgSize];
            canMsg.Data[0] = (byte)(canMsg.Data.Length - 1);
            canMsg.Data[1] = 0x23;
            canMsg.Data[2] = addrDataSize;
            if (modXml.Config.AddressSize + modXml.Config.DataSize > 5)
            {
                // Total UDS payload: 0x23 (1) + addrDataSize (1) + AddressSize + DataSize
                int payloadLength = 2 + modXml.Config.AddressSize + modXml.Config.DataSize;
                byte[] payload = new byte[payloadLength];
                payload[0] = 0x23;
                payload[1] = addrDataSize;
                for (int i = 0; i < modXml.Config.AddressSize; i++)
                    payload[2 + i] = (byte)(xmlMsg.Ident >> (8 * (modXml.Config.AddressSize - 1 - i)));
                for (int i = 0; i < modXml.Config.DataSize; i++)
                    payload[2 + modXml.Config.AddressSize + i] = (byte)(size >> (8 * (modXml.Config.DataSize - 1 - i)));

                // First frame: 2-byte ISO-TP FF header + first 6 bytes of payload
                canMsg.Data = new byte[8];
                canMsg.Data[0] = (byte)(0x10 | (payloadLength >> 8));
                canMsg.Data[1] = (byte)(payloadLength & 0xFF);
                Array.Copy(payload, 0, canMsg.Data, 2, 6);

                // Consecutive frame: 1-byte ISO-TP CF header + remaining payload bytes + padding
                OutputMessage cfMsg = new OutputMessage();
                cfMsg.Data = new byte[8];
                cfMsg.Data[0] = 0x21;
                Array.Copy(payload, 6, cfMsg.Data, 1, payloadLength - 6);
                cfMsg.CanID = canMsg.CanID;
                cfMsg.CanMsgType = canMsg.CanMsgType;
                cfMsg.DLC = 8;
                cfMsg.RxIdent = xmlMsg.RxIdent;
                cfMsg.IsChild = true;
                cfMsg.Linked = true;
                cfMsg.Delay = canMsg.Delay;
                cfMsg.Timeout = 1000;
                cfMsg.ProtocolType = Protocol_Type.UDS;
                cfMsg.Can0 = canMsg.Can0;
                cfMsg.Can1 = canMsg.Can1;
                cfMsg.Can2 = canMsg.Can2;
                cfMsg.Can3 = canMsg.Can3;
                messages.Add(cfMsg);
            }
            else
            {
                for (int i = 0; i < modXml.Config.AddressSize; i++)
                {
                    canMsg.Data[i + 3] = (byte)(xmlMsg.Ident >> (8 * (modXml.Config.AddressSize - 1 - i)));
                }
                for (int i = 0; i < modXml.Config.DataSize; i++)
                {
                    canMsg.Data[i + 3 + modXml.Config.AddressSize] = (byte)(size >> (8 * (modXml.Config.DataSize - 1 - i)));
                }
            }
        }

        static OutputMessage NewOutputUdsMsg(byte[] data, uint delay = 10, uint txId = 0x7E0, uint rxId = 0x7E8, string comment = "")
        {
            OutputMessage canMsg = new();
            canMsg.Delay = delay;
            canMsg.Timeout = 1000;
            canMsg.CanID = txId;
            canMsg.RxIdent = rxId;
            canMsg.IsChild = true;
            canMsg.Data = data;
            canMsg.Name = comment;
            return canMsg;
        }

        static bool IsXcpPollingItem(PollingItem item) => item is not null && item.ShortUpload;

        static void ConfigureXcpMessage(OutputMessage msg, ModuleXml modXml, uint txIdent, uint rxIdent)
        {
            msg.ProtocolType = Protocol_Type.XCP;
            msg.CanID = txIdent;
            msg.RxIdent = rxIdent;
            msg.Timeout = 1000;
            if (modXml.CcpXcpCfg.IsExtended)
                msg.CanMsgType = modXml.CcpXcpCfg.IsCanFd ? DBCMessageType.CanFDExtended : DBCMessageType.Extended;
            else
                msg.CanMsgType = modXml.CcpXcpCfg.IsCanFd ? DBCMessageType.CanFDStandard : DBCMessageType.Standard;
            if (modXml.CcpXcpCfg.IsCanFd)
                msg.BRS = true;
        }

        static void ConfigureBusFlags(OutputMessage msg, byte canBus)
        {
            if (canBus == 1)
                msg.Can1 = true;
            else if (canBus == 2)
                msg.Can2 = true;
            else if (canBus == 3)
                msg.Can3 = true;
            else
                msg.Can0 = true;
        }

        static void AddXcpShortUploadRepeatMessages(List<OutputMessage> messages, ModuleXml modXml)
        {
            foreach (var xmlMsg in modXml.PollingItemList.Items
                         .Where(item => item.Service == ServiceType.XCP && IsXcpPollingItem(item))
                         .GroupBy(x => x.Ident).Select(group => group.First()).OrderBy(x => x.Order))
            {
                OutputMessage canMsg = new()
                {
                    Name = $"SHORT_UPLOAD {xmlMsg.Name}",
                    Data = new byte[8],
                    DLC = 8,
                    Delay = (uint)Math.Max(xmlMsg.Delay, 1)
                };

                uint address = xmlMsg.Ident;
                canMsg.Data[0] = 0xF4;
                canMsg.Data[1] = (byte)(xmlMsg.BitCount / 8);
                canMsg.Data[2] = 0;
                if (modXml.CcpXcpCfg.ByteOrder == A2lParserLib.Enums.ByteOrder.Intel)
                {
                    canMsg.Data[3] = (byte)address;
                    canMsg.Data[4] = (byte)(address >> 8);
                    canMsg.Data[5] = (byte)(address >> 16);
                    canMsg.Data[6] = (byte)(address >> 24);
                }
                else
                {
                    canMsg.Data[3] = (byte)(address >> 24);
                    canMsg.Data[4] = (byte)(address >> 16);
                    canMsg.Data[5] = (byte)(address >> 8);
                    canMsg.Data[6] = (byte)address;
                }

                ConfigureXcpMessage(canMsg, modXml, modXml.CcpXcpCfg.Cro, modXml.CcpXcpCfg.Dto);
                ConfigureBusFlags(canMsg, modXml.Config?.CanBus ?? 0);
                messages.Add(canMsg);
            }
        }

        static void AddCcpUploadRepeatMessages(List<OutputMessage> messages, ModuleXml modXml)
        {
            foreach (var xmlMsg in modXml.PollingItemList.Items
                         .Where(item => item.Service == ServiceType.CCP && IsXcpPollingItem(item))
                         .GroupBy(x => x.Ident).Select(group => group.First()).OrderBy(x => x.Order))
            {
                uint address = xmlMsg.Ident;

                OutputMessage shortUpMsg = new()
                {
                    Name = $"SHORT_UP {xmlMsg.Name}",
                    Data = [0x0F, 0x00, (byte)(xmlMsg.BitCount / 8), 0x00, (byte)address, (byte)(address >> 8), (byte)(address >> 16), (byte)(address >> 24)],
                    DLC = 8,
                    Delay = (uint)Math.Max(xmlMsg.Delay, 1)
                };
                ConfigureXcpMessage(shortUpMsg, modXml, modXml.CcpXcpCfg.Cro, modXml.CcpXcpCfg.Dto);
                ConfigureBusFlags(shortUpMsg, modXml.Config?.CanBus ?? 0);
                messages.Add(shortUpMsg);
            }
        }

        public static void LoadMode2AFromModuleXml(this List<OutputMessage> messages, ModuleXml modXml, List<OutputMessage> repeatMsgList)
        {

            OutputMessage canMsg;
            bool has2AMsgs = false;
            bool isFirstMsg = true;
            ushort dynSize = 0;
            byte addrDataSize = Convert.ToByte($"{modXml.Config.DataSize}{modXml.Config.AddressSize}", 16);
            uint dynSignalStart = modXml.Config.DynamicSignal;

            foreach (var xmlMsg in modXml.PollingItemList.Items.Where(item => item.UDSServiceID == 0x2A)
                                .GroupBy(x => x.Ident).Select(group => group.First()).OrderBy(x => x.Order))
            {
                has2AMsgs = true;
                ushort size = (ushort)(xmlMsg.BitCount / 8);
                dynSize += size;
                if (dynSize > 8 || isFirstMsg)
                {
                    if (dynSize > 8)
                    {
                        dynSignalStart++;
                        dynSize = size;
                    }                    
                    if (isFirstMsg)
                    {
                        messages.Add(NewOutputUdsMsg(new byte[8] { 02, 0x2A, 0x4, 0, 0, 0, 0, 0 }));  //Stop mode 0x2A broadcasting
                    }
                    messages.Add(NewOutputUdsMsg(new byte[8] { 0x4, 0x2C, 3, (byte)(dynSignalStart >> 8), (byte)dynSignalStart, 0, 0, 0 }));  //Register Dynamic Ident 0xF200+                                   
                    isFirstMsg = false;
                }

                messages.Add(NewOutputUdsMsg(new byte[8] { 0x10, 0x0A, 0x2C, 2, (byte)(dynSignalStart >> 8), (byte)dynSignalStart,
                            addrDataSize, (byte)(xmlMsg.Ident >> (8 * (modXml.Config.AddressSize - 1))) }));  //Map addresses to Dynamic Ident 0xF200+
                messages.Add(NewOutputUdsMsg(new byte[8] { 0x21, 0, 0, 0, 0, 0, 0, 0 }));
                for (int i = 1; i < modXml.Config.AddressSize; i++) //skip the HI byte of the address since it was written in the previous message
                {
                    messages.Last().Data[i] = (byte)(xmlMsg.Ident >> (8 * (modXml.Config.AddressSize - 1 - i)));
                }
                for (int i = 0; i < modXml.Config.DataSize; i++)
                {
                    messages.Last().Data[i + modXml.Config.AddressSize] = (byte)(size >> (8 * (modXml.Config.DataSize - 1 - i)));  
                }
            }
            if (!has2AMsgs)
                return;
            for (uint i = modXml.Config.DynamicSignal; i <= dynSignalStart; i++)
            {
                messages.Add(NewOutputUdsMsg(new byte[8] { 03, 0x2A, 0x2, (byte)i, 0, 0, 0, 0 })); //Start broadcasting dynamic ident
            }
            repeatMsgList.Add(NewOutputUdsMsg(new byte[8] { 2, 0x3E, 0, 0, 0, 0, 0, 0 }, 2000)); //Add Tester Present message

        }


        public static void LoadXcpFromModuleXml(this List<OutputMessage> messages, ModuleXml modXml)
        {
            uint txIdent = modXml.CcpXcpCfg.Cro;
            uint rxIdent = modXml.CcpXcpCfg.Dto;
            var byteOrder = modXml.CcpXcpCfg.ByteOrder;
            OutputMessage NewOutputXcpMsg(byte[] data)
            {
                var msg = NewOutputUdsMsg(data, txId: txIdent, rxId: rxIdent, delay : 1);
                msg.ProtocolType = Protocol_Type.XCP;
                if (modXml.CcpXcpCfg.IsExtended)
                    msg.CanMsgType = modXml.CcpXcpCfg.IsCanFd ? DBCMessageType.CanFDExtended : DBCMessageType.Extended;
                else
                    msg.CanMsgType = modXml.CcpXcpCfg.IsCanFd ? DBCMessageType.CanFDStandard : DBCMessageType.Standard;
                if (modXml.CcpXcpCfg.IsCanFd)
                    msg.BRS = true;
                return msg;
            }

            if (modXml.CcpXcpCfg.IsXcp)
            {
                var xcpItems = modXml.PollingItemList.Items.Where(x => x.Service == ServiceType.XCP).ToList();
                var daqItems = xcpItems.Where(x => !IsXcpPollingItem(x)).ToList();
                if (xcpItems.Count == 0)
                    return;

                void AddXcpSeedKeyMessages(byte resource, string unlockFile)
                {
                    if (string.IsNullOrWhiteSpace(unlockFile))
                        return;

                    messages.Add(NewOutputXcpMsg([0xF8, 0, resource, 0, 0, 0, 0, 0])); // GET_SEED
                    messages.Last().Name = $"GET_SEED {unlockFile}";

                    var msg = NewOutputXcpMsg([0xF7, 0, 0, 0, 0, 0, 0, 0]); // UNLOCK
                    msg.Name = "SEND KEY";
                    msg.Delay = 100;
                    messages.Add(msg);
                }

                txIdent = modXml.CcpXcpCfg.Cro;
                rxIdent = modXml.CcpXcpCfg.Dto;
                var daqsWithItems = daqItems.GroupBy(x => x.UDSServiceID).Select(x => x.Key).ToList();

                messages.Add(NewOutputXcpMsg([0xFF, 0, 0, 0, 0, 0, 0, 0])); //Connect
                messages.Add(NewOutputXcpMsg([0xFD, 0, 0, 0, 0, 0, 0, 0])); //GET_STATUS

                if (modXml.CcpXcpCfg.UseSeedKey)
                {
                    AddXcpSeedKeyMessages(0x01, modXml.CcpXcpCfg.SeedFileCal);
                    AddXcpSeedKeyMessages(0x04, modXml.CcpXcpCfg.SeedFileDaq);
                    AddXcpSeedKeyMessages(0x08, modXml.CcpXcpCfg.SeedFileStim);
                    AddXcpSeedKeyMessages(0x10, modXml.CcpXcpCfg.SeedFilePgm);
                }
                if (daqsWithItems.Count == 0)
                    return;
                ushort daqIdx = 0;
                if (modXml.CcpXcpCfg.DaqType == A2lParserLib.Enums.DaqType.Dynamic)
                {
                    messages.Add(NewOutputXcpMsg([0xD6, 0, 0, 0, 0, 0, 0, 0])); //Free Daq
                    if (byteOrder == A2lParserLib.Enums.ByteOrder.Intel)
                        messages.Add(NewOutputXcpMsg([0xD5, 0, (byte)daqsWithItems.Count(), (byte)(daqsWithItems.Count() >> 8), 0, 0, 0, 0])); //Alloc Daq    
                    else
                        messages.Add(NewOutputXcpMsg([0xD5, 0, (byte)(daqsWithItems.Count() >> 8), (byte)daqsWithItems.Count(), 0, 0, 0, 0])); //Alloc Daq    

                    for (ushort i = 0; i < modXml.CcpXcpCfg.Daqs.Count; i++)
                    {
                        if (daqsWithItems.Contains((byte)i))
                        {
                            var groupOdt = daqItems.Where(x => x.UDSServiceID == i).
                                GroupBy(x => x.Mode).Select(group => new { Mode = group.Key, Count = group.Count() }).OrderBy(y => y.Mode);
                            if (modXml.CcpXcpCfg.DaqType == A2lParserLib.Enums.DaqType.Static)  //If daqlist is dynamic then daq lists must be sequential
                                daqIdx = i;
                            if (byteOrder == A2lParserLib.Enums.ByteOrder.Intel)
                                messages.Add(NewOutputXcpMsg([0xD4, 0, (byte)daqIdx, (byte)(daqIdx >> 8), (byte)groupOdt.Count(), 0, 0, 0])); //Alloc number of ODT for every DAQ
                            else
                                messages.Add(NewOutputXcpMsg([0xD4, 0, (byte)(daqIdx >> 8), (byte)daqIdx, (byte)groupOdt.Count(), 0, 0, 0])); //Alloc number of ODT for every DAQ
                            daqIdx++;
                        }
                    }
                    daqIdx = 0;
                    for (ushort i = 0; i < modXml.CcpXcpCfg.Daqs.Count; i++)
                    {
                        if (daqsWithItems.Contains((byte)i))
                        {
                            var groupOdt = daqItems.Where(x => x.UDSServiceID == i).
                                GroupBy(x => x.Mode).Select(group => new { Mode = group.Key, Count = group.Count() }).OrderBy(y => y.Mode);
                            if (modXml.CcpXcpCfg.DaqType == A2lParserLib.Enums.DaqType.Static)  //If daqlist is dynamic then daq lists must be sequential
                                daqIdx = i;
                            byte odtCounter = 0;
                            foreach (var odt in groupOdt)
                            {
                                if (byteOrder == A2lParserLib.Enums.ByteOrder.Intel)
                                    messages.Add(NewOutputXcpMsg([0xD3, 0, (byte)daqIdx, (byte)(daqIdx >> 8), odtCounter, (byte)odt.Count, 0, 0])); //Alloc number of items (ODT_Entry) for every ODT
                                else
                                    messages.Add(NewOutputXcpMsg([0xD3, 0, (byte)(daqIdx >> 8), (byte)daqIdx, odtCounter, (byte)odt.Count, 0, 0])); //Alloc number of items (ODT_Entry) for every ODT
                                odtCounter++;
                            }
                            daqIdx++;
                        }
                    }
                }
                else  //CLEAR_DAQ_LIST for Static DAQ only
                {
                    for (ushort i = 0; i < modXml.CcpXcpCfg.Daqs.Count; i++)
                        if (byteOrder == A2lParserLib.Enums.ByteOrder.Intel)
                            messages.Add(NewOutputXcpMsg([0xE3, 0, (byte)i, (byte)(i >> 8), 0, 0, 0, 0])); // CLEAR_DAQ_LIST
                        else
                            messages.Add(NewOutputXcpMsg([0xE3, 0, (byte)(i >> 8), (byte)i, 0, 0, 0, 0])); // CLEAR_DAQ_LIST
                }

                daqIdx = 0;                //reset counter
                for (ushort i = 0; i < modXml.CcpXcpCfg.Daqs.Count; i++)
                {
                    if (daqsWithItems.Contains((byte)i))
                    {
                        int odtNum = 0;
                        if (modXml.CcpXcpCfg.DaqType == A2lParserLib.Enums.DaqType.Static)  //If daqlist is dynamic then daq lists must be sequential
                            daqIdx = i;
                        int lastMode = -1;
                            var items = daqItems.Where(x => x.UDSServiceID == i).OrderBy(x => x.Mode).ThenBy(x=>x.StartBit).ToList();
                        for (int idx = 0; idx < items.Count; idx++)
                        {
                            if (lastMode != (int)items[idx].Mode)
                            {
                                if (byteOrder == A2lParserLib.Enums.ByteOrder.Intel)
                                    messages.Add(NewOutputXcpMsg([0xE2, 0, (byte)daqIdx, (byte)(daqIdx >> 8), (byte)odtNum, 0, 0, 0])); //Set Daq Pointer (SET_DAQ_PTR)
                                else
                                    messages.Add(NewOutputXcpMsg([0xE2, 0, (byte)(daqIdx >> 8), (byte)daqIdx, (byte)odtNum, 0, 0, 0])); //Set Daq Pointer (SET_DAQ_PTR)
                                lastMode = (int)items[idx].Mode;
                                odtNum++;
                            }
                            uint address = items[idx].Ident;
                            if (byteOrder == A2lParserLib.Enums.ByteOrder.Intel)
                                messages.Add(NewOutputXcpMsg([0xE1, 0xFF, (byte)(items[idx].BitCount / 8), 0,
                                    (byte)address, (byte)(address >> 8), (byte)(address >> 16), (byte)(address >> 24)])); //Write DAQ entry (WRITE_DAQ)
                            else
                                messages.Add(NewOutputXcpMsg([0xE1, 0xFF, (byte)(items[idx].BitCount / 8), 0,
                                    (byte)(address >> 24), (byte)(address >> 16), (byte)(address >> 8), (byte)address])); //Write DAQ entry (WRITE_DAQ)
                        }
                        daqIdx++;
                    }
                }
                daqIdx = 0;
                for (ushort i = 0; i < modXml.CcpXcpCfg.Daqs.Count; i++)
                {
                    if (daqsWithItems.Contains((byte)i))
                    {
                        if (modXml.CcpXcpCfg.DaqType == A2lParserLib.Enums.DaqType.Static)  //If daqlist is dynamic then daq lists must be sequential
                            daqIdx = i;
                        if (byteOrder == A2lParserLib.Enums.ByteOrder.Intel)
                            messages.Add(NewOutputXcpMsg([0xE0, 0, (byte)daqIdx, (byte)(daqIdx >> 8), (byte)daqIdx, (byte)(daqIdx >> 8), 1, 0])); //Assign Event to the DAQ channels SET_DAQ_LIST_MODE
                        else
                            messages.Add(NewOutputXcpMsg([0xE0, 0, (byte)(daqIdx >> 8), (byte)daqIdx, (byte)(daqIdx >> 8), (byte)daqIdx, 1, 0])); //Assign Event to the DAQ channels SET_DAQ_LIST_MODE
                        daqIdx++;
                    }
                }
                daqIdx = 0;
                for (ushort i = 0; i < modXml.CcpXcpCfg.Daqs.Count; i++)
                {
                    if (daqsWithItems.Contains((byte)i))
                    {
                        if (modXml.CcpXcpCfg.DaqType == A2lParserLib.Enums.DaqType.Static)  //If daqlist is dynamic then daq lists must be sequential
                            daqIdx = i;
                        if (byteOrder == A2lParserLib.Enums.ByteOrder.Intel)
                            messages.Add(NewOutputXcpMsg([0xDE, 02, (byte)daqIdx, (byte)(daqIdx >> 8), 0, 0, 0, 0])); //Start DAQ List
                        else
                            messages.Add(NewOutputXcpMsg([0xDE, 02, (byte)(daqIdx >> 8), (byte)daqIdx, 0, 0, 0, 0])); //Start DAQ List
                        daqIdx++;
                    }
                }
                messages.Add(NewOutputXcpMsg([0xDD, 01, 0, 0, 0, 0, 0, 0])); //START_STOP_SYNCH
            }
        }

        public static void LoadCcpFromModuleXml(this List<OutputMessage> messages, ModuleXml modXml)
        {
            uint txIdent = modXml.CcpXcpCfg.Cro;
            uint rxIdent = modXml.CcpXcpCfg.Dto;
            byte cmdCounter = 0;

            OutputMessage NewOutputCcpMsg(byte[] data, string comment = "")
            {
                cmdCounter++;
                var msg = NewOutputUdsMsg(data, txId: txIdent, rxId: rxIdent, delay: 10, comment: comment);
                msg.ProtocolType = Protocol_Type.XCP;
                if (modXml.CcpXcpCfg.IsExtended)
                    msg.CanMsgType = DBCMessageType.Extended;
                else
                    msg.CanMsgType = DBCMessageType.Standard;
                return msg;
            }

            if (!modXml.CcpXcpCfg.IsXcp)
            {
                var ccpItems = modXml.PollingItemList.Items.Where(x => x.Service == ServiceType.CCP).ToList();
                var daqItems = ccpItems.Where(x => !IsXcpPollingItem(x)).ToList();
                if (ccpItems.Count == 0)
                    return;

                txIdent = modXml.CcpXcpCfg.Cro;
                rxIdent = modXml.CcpXcpCfg.Dto;
                var daqsWithItems = daqItems.GroupBy(x => x.UDSServiceID).Select(x => x.Key).ToList();

                messages.Add(NewOutputCcpMsg([0x01, 0, (byte)modXml.CcpXcpCfg.StationAddress, (byte)(modXml.CcpXcpCfg.StationAddress >> 8), 0, 0, 0, 0], "Connect")); //Connect
                messages.Add(NewOutputCcpMsg([0x17, cmdCounter, 0, 0, 0, 0, 0, 0], "Exchange ID")); //Exchange ID
                if (modXml.CcpXcpCfg.UseSeedKey)
                {
                    if (modXml.CcpXcpCfg.SeedFileCal != "")
                    {
                        messages.Add(NewOutputCcpMsg([0x12, cmdCounter, 1, 0, 0, 0, 0, 0], $"GET_SEED {modXml.CcpXcpCfg.SeedFileCal}")); //GET_SEED CAL
                        var msg = NewOutputCcpMsg([0x13, cmdCounter, 0, 0, 0, 0, 0, 0], "SEND KEY");
                        msg.Delay = 100;
                        messages.Add(msg); //SEND KEY
                    }
                    if (modXml.CcpXcpCfg.SeedFileDaq != "")
                    {
                        messages.Add(NewOutputCcpMsg([0x12, cmdCounter, 2, 0, 0, 0, 0, 0], $"GET_SEED {modXml.CcpXcpCfg.SeedFileDaq}")); //GET_SEED DAQ
                        var msg = NewOutputCcpMsg([0x13, cmdCounter, 0, 0, 0, 0, 0, 0], "SEND KEY");
                        msg.Delay = 100;
                        messages.Add(msg); //SEND KEY
                    }
                    if (modXml.CcpXcpCfg.SeedFilePgm != "")
                    {
                        messages.Add(NewOutputCcpMsg([0x12, cmdCounter, 0x40, 0, 0, 0, 0, 0], $"GET_SEED {modXml.CcpXcpCfg.SeedFilePgm}")); //GET_SEED PGM
                        var msg = NewOutputCcpMsg([0x13, cmdCounter, 0, 0, 0, 0, 0, 0], "SEND KEY");
                        msg.Delay = 100;
                        messages.Add(msg); //SEND KEY
                    }
                }                
                if (daqsWithItems.Count == 0)
                    return;

                messages.Add(NewOutputCcpMsg([0x09, cmdCounter, 0, 0, 0, 0, 0, 0])); 
                messages.Add(NewOutputCcpMsg([0x1B, cmdCounter, 2, 1, 0, 0, 0, 0])); 
                messages.Add(NewOutputCcpMsg([0x0C, cmdCounter, 0, 0, 0, 0, 0, 0])); //SET_STATUS
                messages.Add(NewOutputCcpMsg([0x0D, cmdCounter, 0, 0, 0, 0, 0, 0]));
                messages.Add(NewOutputCcpMsg([0x0C, cmdCounter, 0, 0, 0, 0, 0, 0])); //SET_STATUS
                messages.Add(NewOutputCcpMsg([0x17, cmdCounter, 0, 0, 0, 0, 0, 0])); //Exchange ID
                messages.Add(NewOutputCcpMsg([0x04, cmdCounter, 4, 0, 0, 0, 0, 0])); //UPLOAD
                messages.Add(NewOutputCcpMsg([0x0C, cmdCounter, 0, 0, 0, 0, 0, 0])); //SET_STATUS
                for (ushort i = 0; i < modXml.CcpXcpCfg.Daqs.Count; i++)
                {
                    uint daqId = modXml.CcpXcpCfg.Daqs[i].Ident;
                    messages.Add(NewOutputCcpMsg([0x14, cmdCounter, (byte)i, 0, (byte)daqId, (byte)(daqId >> 8), (byte)(daqId >> 16), (byte)(daqId >> 24)], "GET_DAQ_SIZE")); //GET_DAQ_SIZE
                }

                ushort daqIdx = 0;                
                daqIdx = 0;  //reset counter
                Dictionary<byte, byte> daqOdts = new();
                for (ushort i = 0; i < modXml.CcpXcpCfg.Daqs.Count; i++)
                {
                    if (daqsWithItems.Contains((byte)modXml.CcpXcpCfg.Daqs[i].DaqIndex))
                    {
                        int odtIdx = 0;   //Object Descriptor Table ODT number (0,1,...)
                        byte itemIdx = 0;   //Element number within ODT (0,1,...)
                        byte filledSize = 0;
                        var items = daqItems.Where(x => x.UDSServiceID == modXml.CcpXcpCfg.Daqs[i].DaqIndex).OrderBy(x => x.Mode).ThenBy(x => x.StartBit).ToList();
                        for (int idx = 0; idx < items.Count; idx++)
                        {
                            daqIdx = items[idx].UDSServiceID;
                            uint address = items[idx].Ident;
                            byte addrExtension = (address > 0x7FF) ? (byte)1 : (byte)0;
                            byte itemSize = (byte)(items[idx].BitCount / 8);
                            if (filledSize + itemSize <= modXml.CcpXcpCfg.OdtSize)
                            {
                                filledSize += itemSize;
                            }
                            else
                            {
                                filledSize = itemSize;
                                odtIdx++;
                                itemIdx = 0;
                            }                       
                            // Not sure if here it has to be the daq index or the channel ID
                            messages.Add(NewOutputCcpMsg([0x15, cmdCounter, (byte)(daqIdx), (byte)odtIdx, itemIdx, 0, 0, 0], "SET_DAQ_PTR")); //Set Daq Pointer (SET_DAQ_PTR)    
                            messages.Add(NewOutputCcpMsg([0x16, cmdCounter, (byte)(items[idx].BitCount / 8),0 /*addrExtension*/,
                                                (byte)address, (byte)(address >> 8), (byte)(address >> 16), (byte)(address >> 24)], "WRITE_DAQ")); //Write items (WRITE_DAQ)
                            itemIdx++;
                                                     
                        }
                        daqOdts.Add((byte)daqIdx, (byte)odtIdx);  //Write the last ODT for each daq
                    }
                }

                messages.Add(NewOutputCcpMsg([0x0C, cmdCounter, 0x82, 0, 0, 0, 0, 0])); //SET_STATUS
                foreach (var odt in daqOdts)
                {
                    if (modXml.CcpXcpCfg.SynchStartDaqChannels)
                    {
                        messages.Add(NewOutputCcpMsg([0x06, cmdCounter, 2, (byte)odt.Key, (byte)odt.Value, (byte)odt.Key, 0, 1], "START_STOP_DAQ")); //START_STOP_DAQ in Synch mode
                    }
                    else
                    {
                        messages.Add(NewOutputCcpMsg([0x06, cmdCounter, 1, (byte)odt.Key, (byte)odt.Value, (byte)odt.Key, 0, 1], "START_STOP_DAQ")); //START_STOP_DAQ
                    }
                }
                if (modXml.CcpXcpCfg.SynchStartDaqChannels)
                    messages.Add(NewOutputCcpMsg([0x08, cmdCounter, 1, 0, 0, 0, 0, 0], "START_STOP_ALL")); //START_STOP_ALL
            }
        }

        
        public static void SaveToCsv(this List<OutputMessage> messages, string csvFile)
        {
            using (FileStream fs = new FileStream(csvFile, FileMode.Create))
            {
                using (StreamWriter sw = new StreamWriter(fs))
                {
                    string row = "Ident,Linked,Period,Delay,CAN 0,CAN 1,CAN 2,CAN 3,Type,BRS,DLC,Data" + Environment.NewLine;
                    sw.Write(row);
                    foreach (var msg in messages)
                    {
                        row = "0x" + msg.CanID.ToString("X2") + ',';
                        row += msg.Linked && msg.IsChild ? "1," : ',';
                        row += msg.Period > 0 && !msg.IsChild ? msg.Period.ToString() + ',' : ',';
                        row += msg.Delay > 0 && msg.IsChild ? msg.Delay.ToString() + ',' : ',';
                        row += msg.Can0 && !msg.IsChild ? "1," : ',';
                        row += msg.Can1 && !msg.IsChild ? "1," : ',';
                        row += msg.Can2 && !msg.IsChild ? "1," : ',';
                        row += msg.Can3 && !msg.IsChild ? "1," : ',';
                        row += msg.CanMsgType != DBCMessageType.Standard ? ((byte)msg.CanMsgType).ToString() + "," : ',';
                        row += msg.BRS ? "1," : ',';
                        row += msg.DLC.ToString() + ',';
                        for (int i = 0; i < msg.Data.Length; i++)
                        {
                            row += msg.Data[i].ToString("X2") + ' ';
                        }
                        row = row.Trim() + Environment.NewLine;
                        sw.Write(row);
                    }
                }
            }
        }

        public static void ExportToKvaser(this List<OutputMessage> messages, string filename)
        {
            KvaserExporter.WriteLogFile(filename, messages);
        }
    }

}
