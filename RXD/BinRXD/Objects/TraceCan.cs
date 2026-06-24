using RXD.DataRecords;
using System;
using System.Collections.Generic;
using System.Globalization;

namespace RXD.Objects
{
    internal class TraceCan : TraceRow, IRecordGridItem, IItemGridDetails, ITraceConvertAdapter
    {
        public static readonly List<byte> DlcFDList = new List<byte> { 0, 1, 2, 3, 4, 5, 6, 7, 8, 12, 16, 20, 24, 32, 48, 64 };

        public string strItemName => $"{strBusChannel} \\ {strCanID}";

        public string strTimestamp => string.Format(CultureInfo.InvariantCulture, "{0:0.000000}", FloatTimestamp);


        public byte BusChannel { get; set; }

        public string strBusChannel => "CAN " + BusChannel.ToString();

        public UInt32 CanID { get; set; }

        public string strCanID => "0x" + CanID.ToString(flagIDE ? "X8" : "X3");

        public bool flagIDE;
        public bool flagSRR;
        public bool flagEDL;
        public bool flagBRS;
        public bool flagDIR;

        public MessageFlags Flags
        {
            set
            {
                flagIDE = value.HasFlag(MessageFlags.IDE);
                flagSRR = value.HasFlag(MessageFlags.SRR);
                flagEDL = value.HasFlag(MessageFlags.EDL);
                flagBRS = value.HasFlag(MessageFlags.BRS);
                flagDIR = value.HasFlag(MessageFlags.DIR);
            }
        }

        public string strFlags =>
            (flagIDE ? "X" : " ") +
            (flagSRR ? "R" : " ") +
            (flagEDL ? flagBRS ? "FB" : "F " : "  ") +
            (flagDIR ? " Tx" : " Rx");

        public UInt16 DLC { get; set; }
        public UInt16 RawDLC => (UInt16)DlcFDList.IndexOf((byte)DLC);

        public string strDLC => DLC.ToString();
        public byte[] Data;

        public string strData => BitConverter.ToString(Data).Replace("-", " ");

        public virtual string asASCII
        {
            get
            {
                if (NotExportable)
                    return "";

                if (flagEDL)
                    return string.Join(" ",
                        strTimestamp.PadLeft(20),
                        ("CANFD " + (BusChannel + 1).ToString()).PadLeft(10),
                        (CanID.ToString("X8") + (flagIDE ? "x" : " ")).PadLeft(10),
                        (flagDIR ? " Tx" : " Rx").PadLeft(5),
                        ("1 0 d " + (RawDLC.ToString() + " " + DLC.ToString("X").PadLeft(3)).PadLeft(6)).PadLeft(14),
                        strData
                    );
                else
                    return string.Join(" ",
                        strTimestamp.PadLeft(20),
                        (BusChannel + 1).ToString().PadLeft(10),
                        (CanID.ToString("X8") + (flagIDE ? "x" : " ")).PadLeft(10),
                        (flagDIR ? " Tx" : " Rx").PadLeft(5),
                        ("d " + DLC.ToString("X").PadLeft(6)).PadLeft(14),
                        strData
                    );
            }
        }

        public virtual string asTRC
        {
            get
            {
                if (NotExportable)
                    return "";

                return string.Join(" ",
                    string.Format("{0:0.000}", FloatTimestamp * 1000).PadLeft(20),
                    flagEDL ? " FD " : " DT ",
                    (BusChannel + 1).ToString().PadLeft(5),
                    (CanID.ToString(flagIDE ? "X8" : "X4")).PadLeft(10),
                    (flagDIR ? " Tx" : " Rx").PadLeft(5),
                    strDLC.PadLeft(10) + "   ",
                    strData
                );
            }
        }

        public virtual string asCST
        {
            get
            {
                if (NotExportable)
                    return "";

                return string.Join(",",
                    strTimestamp,
                    strBusChannel,
                    strCanID,
                    flagDIR ? "Tx" : "Rx",
                    strDLC,
                    strData
                );
            }
        }
    }
}
