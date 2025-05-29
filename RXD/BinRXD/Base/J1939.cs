using RXD.DataRecords;
using System;

namespace RXD.Base
{
    public static class J1939
    {
        public static UInt32 pgnMFConnect = 0xEC00;
        public static UInt32 pgnMFDataTransfer = 0xEB00;
        public static UInt32 pgnRequest = 0xEA00;
        public static MessageFlags pgnFlagsMask = MessageFlags.BRS | MessageFlags.DIR | MessageFlags.IDE | MessageFlags.SRR;
        public static MessageFlags pgnFlagsValue = MessageFlags.IDE;

        static J1939()
        {

        }

        internal static bool isPgnConnectMessage(RecCanTrace record)
        {
            if (record.data.CanID.PGN != pgnMFConnect)
                return false;

            if (record.header.DLC != 8)
                return false;

            if (record.VariableData[0] != 0x20)
                return false;

            return true;
        }

        internal static bool isPgnDataTransferMessage(RecCanTrace record)
        {
            if (record.data.CanID.PGN != pgnMFDataTransfer)
                return false;

            if (record.header.DLC != 8)
                return false;

            if (record.VariableData[0] == 0)
                return false;

            return true;
        }

    }
}
