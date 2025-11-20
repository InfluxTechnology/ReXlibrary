using System;
using System.Collections.Generic;
using System.Text;

namespace A2lParserLib
{
    public static class Enums
    {
        public enum ItemType : byte { Measurement, Characteristic }
        public enum ByteOrder : byte { Intel, Motorola }
        public enum DataType : byte
        {
            UBYTE,              //UNSIGNED INTEGER - 8 BIT
            SBYTE,              //SIGNED INTEGER - 8 BIT
            UWORD,              //UNSIGNED INTEGER - 16 BIT
            SWORD,              //SIGNED INTEGER - 16 BIT
            ULONG,              //UNSIGNED INTEGER - 32 BIT
            SLONG,              //SIGNED INTEGER - 32 BIT
            A_UINT64,           //UNSIGNED INTEGER - 64 BIT
            A_INT64,            //SIGNED INTEGER - 64 BIT
            FLOAT32_IEEE,       //SINGLE PRECISION FLOAT - 32 BIT
            FLOAT64_IEEE        //DOUBLE PRECISION FLOAT - 64 BIT
        }

        public enum CompuMethodType
        {
            IDENTICAL,          //NO CONVERSION
            LINEAR,             //LINEAR, 2 COEFF WITH SLOPE AND OFFSET
            RAT_FUNC,           //6-COEFF WITH 2ND DEGREE NUMERATOR/DENOMINATOR
            TAB_INTP,           //TABLE WITH INTERPOLATION
            TAB_NOINTP,         //TABLE WITHOUT INTERPOLATION
            TAB_VERB,           //VERBAL TABLE/ENUMERATION
            FORM                //FORMULA WITH OPERATORS AND FUNCTIONS
        }

        public enum XcpTimestamp : byte
        {
            NO_TIMESTAMP = 0,
            SIZE_BYTE = 1,
            SIZE_WORD = 2,
            SIZE_DWORD = 4,
        }

        public enum XcpTimestampResolution : byte
        {
            UNIT_1NS,
            UNIT_10NS,
            UNIT_100NS,
            UNIT_1US,
            UNIT_10US,
            UNIT_100US,
            UNIT_1MS,
            UNIT_10MS,
            UNIT_100MS,
            UNIT_1S
        }

        public enum DaqType : byte { Static, Dynamic}


        public static ushort ToSize(this DataType datatype)
        {
            switch (datatype)
            {
                case DataType.UBYTE:
                case DataType.SBYTE: return 1;
                case DataType.UWORD:
                case DataType.SWORD: return 2;
                case DataType.ULONG:
                case DataType.SLONG: return 4;
                case DataType.A_UINT64:
                case DataType.A_INT64: return 8;
                case DataType.FLOAT32_IEEE: return 4;
                case DataType.FLOAT64_IEEE: return 8;
                default: return 0;
            }
        }

        public static DataType ToDataType(this string value)
        {
            value = value.ToUpper().Replace("SCALAR_", "");
            if (value == "UBYTE" || value == "BYTE" || value == "BOOLEAN") return DataType.UBYTE;
            else if (value == "SBYTE") return DataType.SBYTE;
            else if (value == "UWORD" || value == "WORD") return DataType.UWORD;
            else if (value == "SWORD") return DataType.SWORD;
            else if (value == "ULONG" || value == "LONG") return DataType.ULONG;
            else if (value == "SLONG") return DataType.SLONG;
            else if (value == "A_UINT64") return DataType.A_UINT64;
            else if (value == "A_INT64") return DataType.A_INT64;
            else if (value == "FLOAT32_IEEE") return DataType.FLOAT32_IEEE;
            else if (value == "FLOAT64_IEEE") return DataType.FLOAT64_IEEE;
            else return DataType.UBYTE;
        }

        public static string ToDisplayString(this DataType valType)
        {
            switch (valType)
            {
                case DataType.UBYTE: return "Unsigned Byte";
                case DataType.SBYTE: return "Signed Byte";
                case DataType.UWORD: return "Unsigned Word";
                case DataType.SWORD: return "Signed Word";
                case DataType.ULONG: return "Unsigned Long";
                case DataType.SLONG: return "Signed Long";
                case DataType.FLOAT32_IEEE: return "IEEE Float";
                case DataType.FLOAT64_IEEE: return "IEEE Double";
                default: return "Unknown";
            }
        }

    }

}
