using InfluxShared.Helpers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;

namespace RXD.Blocks
{
    internal class PropertyCollection : Dictionary<string, PropertyData>
    {
        public PropertyCollection()
        {
        }

        #region Overwritten Dictionary methods
        internal dynamic this[string index]
        {
            get => GetProperty(index.ToLowerFastASCII());
            set => SetProperty(index.ToLowerFastASCII(), value);
        }

        internal new bool ContainsKey(string key)
        {
            return base.ContainsKey(key.ToLowerFastASCII());
        }

        internal new bool TryGetValue(string key, out PropertyData data)
        {
            return base.TryGetValue(key.ToLowerFastASCII(), out data);
        }
        #endregion

        internal PropertyData Property(string propName)
        {
            if (TryGetValue(propName, out PropertyData prop))
                return prop;
            else
                return null;
        }

        internal PropertyData Property(Enum propEnum)
        {
            if (TryGetValue(propEnum.ToString(), out PropertyData prop))
                return prop;
            else
                return null;
        }

        internal dynamic GetProperty(string propName)
        {
            if (TryGetValue(propName, out PropertyData prop))
                return prop.Value;
            else
                return null;
        }

        internal void SetProperty(string propName, dynamic propValue)
        {
            if (TryGetValue(propName, out PropertyData prop))
                prop.Value = propValue;
        }

        #region AddProperty methods
        internal new void Add(PropertyData prop)
        {
            base.Add(prop.id, prop);
        }

        internal void AddProperty(string propName, Type propType, dynamic DefaultValue = null)
        {
            if (!ContainsKey(propName))
                Add(new PropertyData(propName, propType, DefaultValue: DefaultValue));
        }

        internal void AddProperty(Enum propEnum, Type propType, dynamic DefaultValue = null)
        {
            AddProperty(propEnum.ToString(), propType, DefaultValue: DefaultValue);
        }

        internal void AddProperty(string propName, Type propType, UInt16 propSubElementCount, dynamic DefaultValue = null)
        {
            if (!ContainsKey(propName))
                Add(new PropertyData(propName, propType, propSubElementCount, DefaultValue: DefaultValue));
        }

        internal void AddProperty(Enum propEnum, Type propType, UInt16 propSubElementCount, dynamic DefaultValue = null)
        {
            AddProperty(propEnum.ToString(), propType, propSubElementCount, DefaultValue: DefaultValue);
        }

        internal void AddProperty(string propName, Type propType, PropertyData propSubElementCountLink, dynamic DefaultValue = null)
        {
            if (!ContainsKey(propName))
                Add(new PropertyData(propName, propType, propSubElementCountLink, DefaultValue: DefaultValue));
        }

        internal void AddProperty(Enum propEnum, Type propType, PropertyData propSubElementCountLink, dynamic DefaultValue = null)
        {
            AddProperty(propEnum.ToString(), propType, propSubElementCountLink, DefaultValue: DefaultValue);
        }

        internal void AddProperty(string propName, Type propType, string propSubElementCountLinkPropertyName, dynamic DefaultValue = null)
        {
            if (!ContainsKey(propName))
                Add(new PropertyData(propName, propType, Property(propSubElementCountLinkPropertyName), DefaultValue: DefaultValue));
        }

        internal void AddProperty(Enum propEnum, Type propType, Enum propSubElementCountLinkPropertyEnum, dynamic DefaultValue = null)
        {
            AddProperty(propEnum.ToString(), propType, propSubElementCountLinkPropertyEnum.ToString(), DefaultValue: DefaultValue);
        }

        #endregion

        internal bool isHelperProperty(string propName)
        {
            if (TryGetValue(propName, out PropertyData prop))
                return this.Any(p => p.Value.PropType == typeof(string) && p.Value.SubElementCount == prop);
            else
                return false;
        }

        internal bool isHelperProperty(Enum propEnum)
        {
            return isHelperProperty(propEnum.ToString());
        }

        internal byte[] ToBytes()
        {
            using (MemoryStream ms = new MemoryStream())
            {
                using (BinaryWriter bw = new BinaryWriter(ms))
                    foreach (KeyValuePair<string, PropertyData> property in this)
                    {
                        byte[] data = property.Value.ToBytes();
                        if (data != null)
                            if (data.Length > 0)
                                bw.Write(data);
                    }
                return ms.ToArray();
            }
        }

        internal void Parse(byte[] buffer)
        {
            using (MemoryStream ms = new MemoryStream(buffer))
            {
                using (BinaryReader br = new BinaryReader(ms))
                    foreach (KeyValuePair<string, PropertyData> property in this)
                    {
                        Type propType = property.Value.PropType.IsEnum ? Enum.GetUnderlyingType(property.Value.PropType) : property.Value.PropType;
                        if (propType == typeof(string))
                            SetProperty(property.Key, new string(br.ReadChars(property.Value.Size)));
                        else if (propType == typeof(string[]))
                        {
                            byte[] stringSizes = property.Value.SubElementSizes?.Value as byte[] ?? Array.Empty<byte>();
                            string[] propdata = new string[stringSizes.Length];
                            for (int i = 0; i < stringSizes.Length; i++)
                                propdata[i] = new string(br.ReadChars(stringSizes[i]));
                            SetProperty(property.Key, propdata);
                        }
                        else if (propType.IsArray)
                        {
                            Type elType = property.Value.PropType.GetElementType();
                            UInt16 elCount = property.Value.SubElementCount.Value;
                            Int32 elSize = property.Value.SubElementSize;
                            Array propdata = Array.CreateInstance(elType, elCount);

                            byte[] data = br.ReadBytes(property.Value.Size);
                            GCHandle h = GCHandle.Alloc(data, GCHandleType.Pinned);
                            IntPtr ptr = h.AddrOfPinnedObject();
                            for (int i = 0; i < elCount; i++)
                            {
                                if (elType.IsEnum)
                                    propdata.SetValue(Enum.ToObject(elType, Marshal.PtrToStructure(ptr, Enum.GetUnderlyingType(elType))), i);
                                else
                                    propdata.SetValue(Marshal.PtrToStructure(ptr, elType), i);
                                ptr += elSize;
                            }
                            h.Free();
                            SetProperty(property.Key, propdata);
                        }
                        else if (propType == typeof(IPAddress))
                        {
                            SetProperty(property.Key, new IPAddress(br.ReadUInt32()));
                        }
                        else
                        {
                            byte[] data = br.ReadBytes(property.Value.Size);
                            GCHandle h = GCHandle.Alloc(data, GCHandleType.Pinned);
                            SetProperty(property.Key, Marshal.PtrToStructure(h.AddrOfPinnedObject(), propType));
                            h.Free();
                        }
                    }
            }
        }       
    }
}
