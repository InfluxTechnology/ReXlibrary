using A2lParserLib;
using A2lParserLib.Items;
using A2lParserLib.CompuMethods;
using InfluxShared.FileObjects;
using System;
using System.Collections.Generic;
using System.Text;
using static A2lParserLib.Enums;

namespace InfluxShared.Helpers
{
    public static class CanSignalHelper
    {
        public static DBCValueType DbcType(this DataType datatype)
        {
            switch (datatype)
            {
                case DataType.UBYTE: 
                case DataType.ULONG:
                case DataType.A_UINT64:
                case DataType.UWORD: return DBCValueType.Unsigned;
                case DataType.SBYTE: 
                case DataType.SWORD: 
                case DataType.SLONG: 
                case DataType.A_INT64: return DBCValueType.Signed;
                case DataType.FLOAT32_IEEE: return DBCValueType.IEEEFloat;
                case DataType.FLOAT64_IEEE: return DBCValueType.IEEEDouble;
                default: return DBCValueType.Unsigned;
            }
        }
        public static DbcItem ToCanSignal(this IA2lItem a2lItem)
        {
            DbcMessage dbcMessage = new DbcMessage();
            dbcMessage.CANID = a2lItem.EcuAddress;
            dbcMessage.DLC = 8;
            dbcMessage.MsgType = DBCMessageType.Standard;
            DbcItem sig = new DbcItem();
            sig.Name = a2lItem.Name;
            sig.Ident = a2lItem.EcuAddress;
            sig.Units = a2lItem.Units;
            sig.StartBit = 0;
            sig.BitCount = (ushort)(a2lItem.DataType.ToSize() * 8);
            sig.ItemType = 1;
            sig.MinValue = a2lItem.MinValue;
            sig.MaxValue = a2lItem.MaxValue;
            sig.ByteOrder = (DBCByteOrder)(byte)a2lItem.ByteOrder;
            sig.Comment = a2lItem.Description;
            sig.Type = DBCSignalType.ModeDependent;
            sig.ValueType = a2lItem.DataType.DbcType();
            sig.Parent = dbcMessage;
            if (a2lItem.CompuMethod is not null)
            {
                if (a2lItem.CompuMethod is RatFunc)
                {
                    sig.Conversion = new();
                    sig.Conversion.Type = ConversionType.Formula;
                    sig.Conversion.Formula.CoeffA = (a2lItem.CompuMethod as RatFunc).Coeffs[0];
                    sig.Conversion.Formula.CoeffB = (a2lItem.CompuMethod as RatFunc).Coeffs[1];
                    sig.Conversion.Formula.CoeffC = (a2lItem.CompuMethod as RatFunc).Coeffs[2];
                    sig.Conversion.Formula.CoeffD = (a2lItem.CompuMethod as RatFunc).Coeffs[3];
                    sig.Conversion.Formula.CoeffE = (a2lItem.CompuMethod as RatFunc).Coeffs[4];
                    sig.Conversion.Formula.CoeffF = (a2lItem.CompuMethod as RatFunc).Coeffs[5];

                }
                else if (a2lItem.CompuMethod is (CompuTable))
                {

                }
            }
            dbcMessage.Signals.Add(sig);
            return sig;
        }
    }
}
