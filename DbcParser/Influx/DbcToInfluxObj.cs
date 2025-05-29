using InfluxShared.FileObjects;
using InfluxShared.Helpers;
using SharedObjects;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace DbcParserLib.Influx
{
    public static class DbcToInfluxObj
    {
        public static DBC FromList(List<ICanMessage> messages)
        {
            DBC influxDBC = new DBC();
            foreach (var msg in messages)
            {
                if (msg.CANID == 0xC0000000)
                    continue;
                DbcMessage msgI = new DbcMessage();
                msgI.CANID = msg.CANID;
                msgI.DLC = msg.DLC;
                msgI.Comment = msg.Comment;
                msgI.Name = msg.Name;
                msgI.MsgType = msg.MsgType;
                msgI.Transmitter = msg.Transmitter;

                influxDBC.Messages.Add(msgI);
                foreach (var sig in msg.Signals)
                {
                    DbcItem sigI = new DbcItem();
                    sigI.Name = sig.Name;
                    sigI.Comment = sig.Comment;
                    sigI.ByteOrder = sig.ByteOrder == 0 ? DBCByteOrder.Motorola : DBCByteOrder.Intel;
                    sigI.StartBit = sig.StartBit;
                    sigI.BitCount = sig.BitCount;
                    sigI.Units = sig.Units;
                    sigI.MinValue = sig.MinValue;
                    sigI.MaxValue = sig.MaxValue;
                    sigI.Conversion.Type = ConversionType.Formula;
                    sigI.Conversion.Formula.CoeffB = sig.Factor;
                    sigI.Conversion.Formula.CoeffC = sig.Offset;
                    sigI.Conversion.Formula.CoeffF = 1;
                    sigI.Type = sig.Type;
                    sigI.ValueType = sig.ValueType;
                    sigI.ItemType = 0;
                    sigI.Ident = sig.Ident;
                    sigI.Parent = msgI;
                    sigI.Mode = sig.Mode;// Multiplexing

                    msgI.Items.Add(sigI);
                }

            }
            return influxDBC;
        }

        public static DBC FromDBC(Dbc dbc)
        {
            DBC influxDBC = new DBC();
            influxDBC.FileType = dbc.FileType;

            foreach (var msg in dbc.Messages)
            {
                if (msg.ID == 0xC0000000)
                    continue;
                DbcMessage msgI = new DbcMessage();
                msgI.CANID = msg.ID;
                msgI.DLC = msg.DLC;
                msgI.Comment = msg.Comment;
                msgI.Name = msg.Name;
                msgI.MsgType = msg.Type;
                msgI.Transmitter = msg.Transmitter;

                if (dbc.FileType == DBCFileType.KanCan)
                {
                    msgI.CANID = msg.ID & CanIdentifier.KanCanIdMask;
                    msgI.MsgType = DBCMessageType.KanCan;
                }

                influxDBC.Messages.Add(msgI);
                foreach (var sig in msg.Signals)
                {
                    DbcItem sigI = new DbcItem();
                    sigI.Name = sig.Name;
                    sigI.Comment = sig.Comment;
                    sigI.ByteOrder = sig.ByteOrder == 0 ? DBCByteOrder.Motorola : DBCByteOrder.Intel;
                    sigI.StartBit = sig.StartBit;
                    sigI.BitCount = sig.Length;
                    sigI.Units = sig.Unit;
                    sigI.MinValue = sig.Minimum;
                    sigI.MaxValue = sig.Maximum;
                    sigI.Conversion.Type = ConversionType.Formula;
                    sigI.Conversion.Formula.CoeffB = sig.Factor;
                    sigI.Conversion.Formula.CoeffC = sig.Offset;
                    sigI.Conversion.Formula.CoeffF = 1;
                    if (sig.TableNumeric is not null && sig.TableNumeric.Count > 0)
                    {
                        sigI.Conversion.Type = ConversionType.FormulaAndTableNumeric;
                        foreach (var pair in sig.TableNumeric)
                            sigI.Conversion.TableNumeric.Add(pair.Key, pair.Value);
                    }
                    else if (sig.TableVerbal is not null && sig.TableVerbal.Count > 0)
                    {
                        sigI.Conversion.Type = ConversionType.FormulaAndTableVerbal;
                        foreach (var pair in sig.TableVerbal)
                            sigI.Conversion.TableVerbal.Add(pair.Key, pair.Value);

                    }
                    sigI.Type = DBCSignalType.Standard;
                    if (sig.Multiplexing is not null && sig.Multiplexing.Length > 0)
                    {
                        var mpstr = sig.Multiplexing.ToLower();
                        if (mpstr[0] == 'm')
                        {
                            if (mpstr.Length > 1)
                            {
                                mpstr = mpstr.Substring(1);
                                if (int.TryParse(mpstr, out int mode))
                                {
                                    sigI.Mode = (uint)mode;
                                    sigI.Type = DBCSignalType.ModeDependent;
                                }
                            }
                            else
                                sigI.Type = DBCSignalType.Mode;
                        }
                    }
                    sigI.ValueType = sig.ValueType;
                    sigI.ItemType = 0;
                    sigI.Ident = msg.ID;
                    sigI.Parent = msgI;


                    if (msgI.Name == "Mode_0x22")
                        sigI.UDS = 0x22;
                    else if (msgI.Name == "Mode_0x23")
                        sigI.UDS = 0x23;
                    else
                        sigI.UDS = 0;

                    msgI.Items.Add(sigI);
                }

            }
            return influxDBC;
        }

        // Each dbc index in the list is for the corresponding CAN channel
        // So list[0] is for channel 0, list[1] is for channel 1 etc.
        public static ExportDbcCollection LoadExportSignalsFromDBC(List<DBC?> dbcList)
        {
            ExportDbcCollection signalsCollection = new ExportDbcCollection();
            for (byte i = 0; i < dbcList.Count; i++)
            {
                DBC? dbc = dbcList[i];
                if (dbc != null)
                {
                    foreach (var msg in dbc.Messages)
                    {
                        var expmsg = signalsCollection.AddMessage(i, msg);
                        foreach (var sig in msg.Items)
                            if (sig is DbcItem dbcsignal)
                                expmsg.AddSignal(dbcsignal);
                    }
                }
            }
            return signalsCollection;
        }

        static NumberFormatInfo nfi = new NumberFormatInfo() { NumberDecimalSeparator = ".", NumberGroupSeparator = "" };
        static string DefaultNode = "Influx_Node";

        static void WriteNodes(this DBC dbc, StreamWriter sw)
        {
            sw.WriteLine("");
            string line = "BU_: " + (dbc.NodeNames.Count == 0 ? DefaultNode : dbc.NodeNames[0]);
            sw.WriteLine(line);
        }

        static string ModeStr(this ICanSignal sg) => sg.Type switch
        {
            DBCSignalType.Standard => "",
            DBCSignalType.Mode => "M",
            DBCSignalType.ModeDependent => $"m{sg.Mode}"
        };

        static void WriteMessages(this DBC dbc, StreamWriter sw)
        {
            foreach (DbcMessage msg in dbc.Messages)
            {
                sw.WriteLine(
                    Environment.NewLine +
                    $"BO_ {msg.CANID} {msg.Name.DbcNameClean()}: {msg.DLC} {msg.Transmitter ?? (dbc.NodeNames.Count == 0 ? DefaultNode : dbc.NodeNames[0])}"
                );

                foreach (DbcItem sig in msg.Items)
                    sw.WriteLine(
                        $"  SG_ {sig.Name.DbcNameClean()} {sig.ModeStr()} : {sig.StartBit}|{sig.BitCount}@" +
                        (sig.ByteOrder == DBCByteOrder.Intel ? "1" : "0") +
                        (sig.ValueType == DBCValueType.Unsigned ? "+" : "-") +
                        $"({sig.Factor.ToString(nfi)},{sig.Offset.ToString(nfi)}) [{sig.MinValue.ToString(nfi)}|{sig.MaxValue.ToString(nfi)}]" +
                        $" \"{sig.Units}\" {(dbc.NodeNames.Count == 0 ? DefaultNode : dbc.NodeNames[0])}"
                    );
            }
        }

        static void WriteComments(this DBC dbc, StreamWriter sw)
        {
            sw.WriteLine("");

            foreach (DbcMessage msg in dbc.Messages)
            {
                if (msg.Comment is not null && msg.Comment != "")
                    sw.WriteLine(
                        $"CM_ BO_ {msg.CANID} \"{msg.Comment}\";"
                    );

                foreach (DbcItem sig in msg.Items)
                    if (sig.Comment is not null && sig.Comment != "")
                        sw.WriteLine(
                            $"CM_ SG_ {msg.CANID} {sig.Name.DbcNameClean()} \"{sig.Comment}\";"
                        );
            }
        }

        static void WriteEnumValues(this DBC dbc, StreamWriter sw)
        {
            sw.WriteLine("");

            foreach (DbcMessage msg in dbc.Messages)
                foreach (DbcItem sig in msg.Items)
                    if (sig.Conversion.Type == ConversionType.FormulaAndTableVerbal)
                    {
                        sw.Write($"VAL_ {msg.CANID} {sig.Name.DbcNameClean()}");

                        foreach (KeyValuePair<double, string> pair in sig.Conversion.TableVerbal.Pairs)
                            sw.Write($" {pair.Key} \"{pair.Value}\"");
                        sw.WriteLine(";");
                    }
        }

        static void WriteFloatSignals(this DBC dbc, StreamWriter sw)
        {
            sw.WriteLine("");

            foreach (DbcMessage msg in dbc.Messages)
                foreach (DbcItem sig in msg.Items)
                    if (sig.ValueType == DBCValueType.IEEEFloat || sig.ValueType == DBCValueType.IEEEDouble)
                        sw.WriteLine(
                            $"SIG_VALTYPE_ {msg.CANID} {sig.Name.DbcNameClean()} : {(sig.ValueType == DBCValueType.IEEEFloat ? "1" : "2")};"
                        );
        }

        static public void SetType(this DBC dbc, DBCFileType FileType)
        { 
            dbc.FileType = FileType;
            switch (FileType)
            {
                case DBCFileType.Generic:
                    break;
                case DBCFileType.CAN:
                    break;
                case DBCFileType.CANFD:
                    break;
                case DBCFileType.LIN:
                    break;
                case DBCFileType.J1939:
                    break;
                case DBCFileType.Ethernet:
                    break;
                case DBCFileType.FlexRay:
                    break;
                case DBCFileType.KanCan:
                    break;
                default:
                    break;
            }
        }

        static public void SaveDBCToFile(this DBC dbc, string FileName)
        {
            using (StreamWriter sw = new StreamWriter(FileName))
            {
                sw.WriteLine(@"VERSION """"");
                sw.WriteLine("");
                sw.WriteLine("NS_:");
                sw.WriteLine("");
                sw.WriteLine("BS_:");

                dbc.WriteNodes(sw);
                dbc.WriteMessages(sw);
                dbc.WriteComments(sw);

                dbc.WriteEnumValues(sw);
                dbc.WriteFloatSignals(sw);
            }
        }
    }
}
