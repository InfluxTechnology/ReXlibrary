using InfluxShared.Generic;
using InfluxShared.Helpers;
using System;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;

namespace RXD.Blocks
{
    internal class PropertyData
    {
        public readonly string id;

        public string Name;

        public string XmlSequenceGroup = "";

        public Type PropType;

        public PropertyData SubElementCount = null;

        public object Data = null;

        public dynamic DefaultValue = null;
        public dynamic Value
        {
            get => Data;
            set
            {
                if (value.GetType() == PropType)
                    Data = value;
                else if (PropType.IsEnum)
                    Data = Enum.ToObject(PropType, value);
                else
                    Data = Convert.ChangeType(value, PropType);

                if (PropType == typeof(string) && SubElementCount != null)
                    if (SubElementCount.Name == string.Empty)
                        Data = (Data as string).PadRight(SubElementCount.Value, '0').Substring(0, SubElementCount.Value);
                    else
                        SubElementCount.Value = ((string)value).Length;
            }
        }

        public bool IsDefault => Data?.Equals(DefaultValue);

        public Int32 Size
        {
            get
            {
                if (PropType == typeof(string))
                    return (SubElementCount == null) ? 0 : SubElementCount.Value;
                else if (PropType.IsArray)
                    return (SubElementCount == null) ? 0 : SubElementCount.Value * SubElementSize;
                else
                    return GetTypeSize(PropType);
            }
        }

        public Int32 SubElementSize => GetTypeSize(PropType.GetElementType());

        internal Int32 GetTypeSize(Type pType)
        {
            Type t = pType.IsEnum ? Enum.GetUnderlyingType(pType) : pType;
            if (t == typeof(bool))
                t = typeof(byte);
            return Marshal.SizeOf(t);
        }

        #region Constructors and Initializers
        public PropertyData(string propName, Type propType, dynamic DefaultValue = null)
        {
            id = propName.ToLowerFastASCII();
            PropType = propType;
            InitProperty(propName, propType, DefaultValue: DefaultValue);

        }

        public PropertyData(string propName, Type propType, UInt16 propSubElementCount, dynamic DefaultValue = null)
        {
            PropertyData sub = new PropertyData("", typeof(UInt16))
            {
                Value = propSubElementCount
            };

            id = propName.ToLowerFastASCII();
            InitProperty(propName, propType, sub, DefaultValue: DefaultValue);
        }

        public PropertyData(string propName, Type propType, PropertyData propSubElementCountLink, dynamic DefaultValue = null)
        {
            id = propName.ToLowerFastASCII();
            InitProperty(propName, propType, propSubElementCountLink, DefaultValue: DefaultValue);
        }

        void InitProperty(string propName, Type propType, PropertyData propSubElementCountLink = null, dynamic DefaultValue = null)
        {
            dynamic CreateDefault()
            {
                if (PropType == typeof(string))
                    return string.Empty;
                else if (PropType.IsArray)
                    return Array.CreateInstance(PropType.GetElementType(), SubElementCount.Value);
                else if (PropType == typeof(IPAddress))
                    return IPAddress.Parse("0.0.0.0");
                else
                    return Activator.CreateInstance(PropType);
            }

            Name = propName;
            PropType = propType;
            SubElementCount = propSubElementCountLink;

            Data = CreateDefault();
            if (DefaultValue != null)
                Value = this.DefaultValue = DefaultValue;
        }
        #endregion

        public byte[] ToBytes()
        {
            if (Data == null)
                return null;

            if (PropType == typeof(string))
                return Encoding.ASCII.GetBytes(Value);
            else if (PropType.IsArray)
                if (PropType.GetElementType().IsEnum)
                    return Bytes.EnumArrayToBytes(Data);
                else if (PropType.GetElementType() == typeof(bool))
                    return Bytes.BoolArrayToBytes(Data);
                else
                    return Bytes.ArrayToBytes(Data);
            else if (PropType == typeof(IPAddress))
                return Value.GetAddressBytes();
            else
            {
                object obj = PropType.IsEnum ? Convert.ChangeType(Data, Enum.GetUnderlyingType(PropType)) : Data;
                if (obj.GetType() == typeof(bool))
                    obj = Convert.ChangeType(obj, typeof(byte));
                return Bytes.ObjectToBytes(obj);
            }
        }

    }
}
