using InfluxShared.Generic;
using InfluxShared.Helpers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;

namespace InfluxShared.FileObjects
{

    public class OutputMessage
    {
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
                        AddMode23Msg(modXml, canMsg, xmlMsg);
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

                return true;
            }
            catch (Exception exc)
            {
                //LastError = $"Error parsing csv row {rowCounter} Error: {exc.Message}";
                return false;
            }
        }

        private static void AddMode23Msg(ModuleXml modXml, OutputMessage canMsg, PollingItem xmlMsg)
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
            for (int i = 0; i < modXml.Config.AddressSize; i++)
            {
                canMsg.Data[i + 3] = (byte)(xmlMsg.Ident >> (8 * (modXml.Config.AddressSize - 1 - i)));
            }
            for (int i = 0; i < modXml.Config.DataSize; i++)
            {
                canMsg.Data[i + 3 + modXml.Config.AddressSize] = (byte)(size >> (8 * (modXml.Config.DataSize - 1 - i)));
            }
        }

        static OutputMessage NewOutputUdsMsg(byte[] data, uint delay = 10, uint txId = 0x7E0, uint rxId = 0x7E8)
        {
            OutputMessage canMsg = new();
            canMsg.Delay = delay;
            canMsg.Timeout = 1000;
            canMsg.CanID = txId;
            canMsg.RxIdent = rxId;
            canMsg.IsChild = true;
            canMsg.Data = data;
            return canMsg;
        }

        public static void LoadMode2AFromModuleXml(this List<OutputMessage> messages, ModuleXml modXml, List<OutputMessage> repeatMsgList)
        {

            OutputMessage canMsg;
            bool has2AMsgs = false;
            bool isFirstMsg = true;
            ushort dynSize = 0;
            byte addrDataSize = Convert.ToByte($"{modXml.Config.DataSize}{modXml.Config.AddressSize}", 16);
            uint dynIdentStart = modXml.Config.DynIdentStart;
            uint txIdent = 0x7E0;

            foreach (var xmlMsg in modXml.PollingItemList.Items.Where(item => item.UDSServiceID == 0x2A)
                                .GroupBy(x => x.Ident).Select(group => group.First()).OrderBy(x => x.Order))
            {
                has2AMsgs = true;
                txIdent = xmlMsg.TxIdent;
                ushort size = (ushort)(xmlMsg.BitCount / 8);
                dynSize += size;
                if (dynSize > 8 || isFirstMsg)
                {
                    if (dynSize > 8)
                    {
                        dynIdentStart++;
                        dynSize = size;
                    }                    
                    if (isFirstMsg)
                    {
                        messages.Add(NewOutputUdsMsg(new byte[8] { 02, 0x2A, 0x4, 0, 0, 0, 0, 0 }));  //Stop mode 0x2A broadcasting
                    }
                    messages.Add(NewOutputUdsMsg(new byte[8] { 0x4, 0x2C, 3, (byte)(dynIdentStart >> 8), (byte)dynIdentStart, 0, 0, 0 }));  //Register Dynamic Ident 0xF200+                                   
                    isFirstMsg = false;
                }

                messages.Add(NewOutputUdsMsg(new byte[8] { 0x10, 0x0A, 0x2C, 2, (byte)(dynIdentStart >> 8), (byte)dynIdentStart,
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
            for (uint i = modXml.Config.DynIdentStart; i <= dynIdentStart; i++)
            {
                messages.Add(NewOutputUdsMsg(new byte[8] { 03, 0x2A, 0x2, (byte)i, 0, 0, 0, 0 })); //Start broadcasting dynamic ident
            }
            repeatMsgList.Add(NewOutputUdsMsg(new byte[8] { 2, 0x3E, 0, 0, 0, 0, 0, 0 }, 2000)); //Add Tester Present message

        }


        public static void LoadXcpFromModuleXml(this List<OutputMessage> messages, ModuleXml modXml)
        {
            uint txIdent = modXml.XcpCfg.Cro;
            uint rxIdent = modXml.XcpCfg.Dto; 

            OutputMessage NewOutputXcpMsg(byte[] data)
            {
                var msg = NewOutputUdsMsg(data, txId: txIdent, rxId: rxIdent, delay : 1);
                if (modXml.XcpCfg.IsExtended)
                    msg.CanMsgType = modXml.XcpCfg.IsCanFd ? DBCMessageType.CanFDExtended : DBCMessageType.Extended;
                else
                    msg.CanMsgType = modXml.XcpCfg.IsCanFd ? DBCMessageType.CanFDStandard : DBCMessageType.Standard;
                if (modXml.XcpCfg.IsCanFd)
                    msg.BRS = true;
                return msg;
            }

            if (modXml.XcpCfg.Daqs.Count > 0)
            {
                txIdent = modXml.XcpCfg.Cro;
                rxIdent = modXml.XcpCfg.Dto;
                var daqsWithItems = modXml.PollingItemList.Items.Where(x => x.Service == ServiceType.XCP).GroupBy(x => x.UDSServiceID).Select(x=>x.Key).ToList();

                messages.Add(NewOutputXcpMsg([0xFF, 0, 0, 0, 0, 0, 0, 0])); //Connect
                messages.Add(NewOutputXcpMsg([0xD6, 0, 0, 0, 0, 0, 0, 0])); //Free Daq    
                messages.Add(NewOutputXcpMsg([0xD5, 0, (byte)daqsWithItems.Count(), (byte)(daqsWithItems.Count() >> 8), 0, 0, 0, 0])); //Alloc Daq    

                /*for (ushort i = 0; i < modXml.XcpCfg.Daqs.Count; i++)
                {
                    if (daqsWithItems.Contains((byte)i))
                    {
                        var items = modXml.PollingItemList.Items.Where(x => x.Service == ServiceType.XCP && x.UDSServiceID == i).ToList();
                        messages.Add(NewOutputXcpMsg([0xD4, 0, (byte)i, (byte)(i >> 8), (byte)daqsWithItems.Count, 0, 0, 0])); //Alloc number of ODT for every DAQ
                    }
                }*/
                ushort daqIdx = 0;
                for (ushort i = 0; i < modXml.XcpCfg.Daqs.Count; i++)
                {
                    if (daqsWithItems.Contains((byte)i))
                    {
                        var groupOdt = modXml.PollingItemList.Items.Where(x => x.Service == ServiceType.XCP && x.UDSServiceID == i).
                            GroupBy(x => x.Mode).Select(group => new { Mode = group.Key, Count = group.Count() }).OrderBy(y => y.Mode);
                        if (modXml.XcpCfg.DaqType == A2lParserLib.Enums.DaqType.Static)  //If daqlist is dynamic then daq lists must be sequential
                            daqIdx = i;
                        messages.Add(NewOutputXcpMsg([0xD4, 0, (byte)daqIdx, (byte)(daqIdx >> 8), (byte)groupOdt.Count(), 0, 0, 0])); //Alloc number of ODT for every DAQ                        
                        daqIdx++;
                    }
                }
                daqIdx = 0;
                for (ushort i = 0; i < modXml.XcpCfg.Daqs.Count; i++)
                {
                    if (daqsWithItems.Contains((byte)i))
                    {
                        var groupOdt = modXml.PollingItemList.Items.Where(x => x.Service == ServiceType.XCP && x.UDSServiceID == i).
                            GroupBy(x => x.Mode).Select(group => new { Mode = group.Key, Count = group.Count() }).OrderBy(y=>y.Mode);
                        if (modXml.XcpCfg.DaqType == A2lParserLib.Enums.DaqType.Static)  //If daqlist is dynamic then daq lists must be sequential
                            daqIdx = i;
                        byte odtCounter = 0;
                        foreach (var odt in groupOdt)
                        {
                            messages.Add(NewOutputXcpMsg([0xD3, 0, (byte)daqIdx, (byte)(daqIdx >> 8), odtCounter, (byte)odt.Count, 0, 0])); //Alloc number of items (ODT_Entry) for every ODT
                            odtCounter++;
                        }
                        daqIdx++;
                    }
                }
                daqIdx = 0;  //reset counter
                for (ushort i = 0; i < modXml.XcpCfg.Daqs.Count; i++)
                {
                    if (daqsWithItems.Contains((byte)i))
                    {
                        int odtNum = 0;
                        if (modXml.XcpCfg.DaqType == A2lParserLib.Enums.DaqType.Static)  //If daqlist is dynamic then daq lists must be sequential
                            daqIdx = i;
                        int lastMode = -1;
                            var items = modXml.PollingItemList.Items.Where(x => x.Service == ServiceType.XCP && x.UDSServiceID == i).OrderBy(x => x.Mode).ThenBy(x=>x.StartBit).ToList();
                        for (int idx = 0; idx < items.Count; idx++)
                        {
                            if (lastMode != (int)items[idx].Mode)
                            {
                                messages.Add(NewOutputXcpMsg([0xE2, 0, (byte)daqIdx, (byte)(daqIdx >> 8), (byte)odtNum, 0, 0, 0])); //Set Daq Pointer (SET_DAQ_PTR)    
                                lastMode = (int)items[idx].Mode;
                                odtNum++;
                            }
                            uint address = items[idx].Ident;
                            messages.Add(NewOutputXcpMsg([ 0xE1, 0xFF, (byte)(items[idx].BitCount / 8), 0,
                                    (byte)address, (byte)(address >> 8), (byte)(address >> 16), (byte)(address >> 24) ])); //Alloc number of ODT for every DAQ
                        }
                        daqIdx++;
                    }
                }
                daqIdx = 0;
                for (ushort i = 0; i < modXml.XcpCfg.Daqs.Count; i++)
                {
                    if (daqsWithItems.Contains((byte)i))
                    {
                        if (modXml.XcpCfg.DaqType == A2lParserLib.Enums.DaqType.Static)  //If daqlist is dynamic then daq lists must be sequential
                            daqIdx = i;
                        messages.Add(NewOutputXcpMsg([0xE0, 0, (byte)daqIdx, (byte)(daqIdx >> 8), (byte)i, 0, 1, 0])); //Assign Event to the DAQ channels SET_DAQ_LIST_MODE
                        daqIdx++;
                    }
                }
                daqIdx = 0;
                for (ushort i = 0; i < modXml.XcpCfg.Daqs.Count; i++)
                {
                    if (daqsWithItems.Contains((byte)i))
                    {
                        if (modXml.XcpCfg.DaqType == A2lParserLib.Enums.DaqType.Static)  //If daqlist is dynamic then daq lists must be sequential
                            daqIdx = i;
                        messages.Add(NewOutputXcpMsg([0xDE, 02, (byte)daqIdx, (byte)(daqIdx >> 8), 0, 0, 0, 0])); //Start DAQ List
                        daqIdx++;
                    }
                }
                messages.Add(NewOutputXcpMsg([0xDD, 01, 0, 0, 0, 0, 0, 0])); //START_STOP_SYNCH
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


    }

}
