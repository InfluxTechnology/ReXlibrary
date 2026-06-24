/* ------------------------------------
 * Author:  Georgi Georgiev
 * Year:    03.2024
 * ------------------------------------
 */

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Xml;


namespace InfluxShared.FileObjects
{
    public class XML
    {

        public List<ICanMessage> CANMessages { get; set; }

        public string FileName { get; set; }

        public XML()
        {
            CANMessages = new List<ICanMessage>();
        }

        private bool isCondOk(XmlNode node, string text, bool byName, string byAttr)
        {
            if (byAttr != "")
                return AttrByName(node, byAttr).ToUpper() == text.ToUpper();

            if (byName)
            {
                if (node.Name.ToUpper() == text.ToUpper())
                    return true;
            }
            else if (node.InnerText.ToString() == text)
                return true;

            return false;
        }

        public string RefFromFullRef(XmlNode Ref)
        {
            if (Ref == null)
                return "";

            string[] words = Ref.InnerText.ToString().Split('/');
            return words[words.Length - 1];
        }

        public void CompuMethodContent(XmlNode node, ICanSignal sig)
        {
            string category = strContent(node, "CATEGORY");
            bool flagLinear = category.Contains("LINEAR");
            bool flagTable = category.Contains("TEXTTABLE");

            sig.Conversion.Formula.CoeffB = 1;
            if (category.ToUpper() == "IDENTICAL")
            {
                sig.MaxValue = Math.Pow(2, sig.BitCount);
                if (sig.ValueType != DBCValueType.Unsigned)
                {
                    sig.MaxValue = sig.MaxValue / 2;
                    sig.MinValue = sig.MaxValue * (-1);
                }
                return;
            }

            if (flagLinear && !flagTable)
                sig.Conversion.Type = ConversionType.Formula;
            if (/*flagLinear &&*/ flagTable)
                sig.Conversion.Type = ConversionType.FormulaAndTableVerbal;

            node = XmlNode(node, "COMPU-INTERNAL-TO-PHYS");
            node = XmlNode(node, "COMPU-SCALES");
            if (node == null)
                return;

            foreach (XmlNode child in node.ChildNodes)
            {
                double dbl;
                XmlNode node1 = XmlNode(child, "COMPU-RATIONAL-COEFFS");

                if ((node1 != null) && (flagLinear))
                {
                    sig.Conversion.Formula.CoeffC = doubleContent(XmlNode(node1, "COMPU-NUMERATOR"), "V", 0.0, 0);
                    sig.Conversion.Formula.CoeffB = doubleContent(XmlNode(node1, "COMPU-NUMERATOR"), "V", 1.0, 1);

                    dbl = doubleContent(XmlNode(node1, "COMPU-DENOMINATOR"), "V", 1.0);
                    sig.Conversion.Formula.CoeffC = sig.Conversion.Formula.CoeffC / dbl;
                    sig.Conversion.Formula.CoeffB = sig.Conversion.Formula.CoeffB / dbl;

                    sig.MinValue = doubleContent(child, "LOWER-LIMIT");
                    sig.MaxValue = doubleContent(child, "UPPER-LIMIT");
                }

                if ((node1 == null) && (flagTable))
                {
                    var s = strContent(XmlNode(child, "COMPU-CONST"), "VT");
                    //dbl = doubleContent(child, "LOWER-LIMIT");
                    uint ll = uintContent(child, "LOWER-LIMIT");
                    uint ul = uintContent(child, "UPPER-LIMIT");
                    for (uint i = ll; i <= ul; i++)
                        sig.Conversion.TableVerbal.Pairs[i] = s;
                }
            }
        }

        public double ConvertToDouble(string s, double def)
        {
            char systemSeparator = Thread.CurrentThread.CurrentCulture.NumberFormat.CurrencyDecimalSeparator[0];

            double val = def;
            if (s == "")
                return val;
            if (!s.Contains(","))
                val = double.Parse(s, CultureInfo.InvariantCulture);
            else
                val = Convert.ToDouble(s.Replace(".", systemSeparator.ToString()).Replace(",", systemSeparator.ToString()));
            return val;
        }

        public double doubleContent(XmlNode node, string text, double def = 0, uint idx = 0)
        {
            if (node == null)
                return def;

            uint i = 0;
            foreach (XmlNode child in node.ChildNodes)
                if (child.Name.ToUpper() == text.ToUpper())
                {
                    if (i == idx)
                        return ConvertToDouble(child.InnerText.ToString(), def);

                    i++;
                }

            return def;
        }

        public string strContent(XmlNode node, string text)
        {
            if (node == null)
                return "";

            foreach (XmlNode child in node.ChildNodes)
                if (child.Name.ToUpper() == text.ToUpper())
                    return child.InnerText.ToString();

            return "";
        }

        public uint uintContent(XmlNode node, string text)
        {
            if (node == null)
                return 0;

            foreach (XmlNode child in node.ChildNodes)
                if (child.Name.ToUpper() == text.ToUpper())
                {
                    string s = child.InnerText.Replace("-", string.Empty);
                    if (uint.TryParse(s, out uint result))
                        return result;
                    return 0;
                }

            return 0;
        }
        public uint hexContent(XmlNode node, string text)
        {
            if (node == null)
                return 0;

            foreach (XmlNode child in node.ChildNodes)
                if (child.Name.ToUpper() == text.ToUpper())
                {
                    string hex = child.InnerText.ToString();
                    hex = hex.Replace("0x", string.Empty);
                    return UInt32.Parse(hex, System.Globalization.NumberStyles.HexNumber);
                }
            return 0;
        }

        public string AttrByName(XmlNode node, string name)
        {
            if (node == null)
                return "";
            if (node.Attributes == null)
                return "";

            for (var i = 0; i < node.Attributes.Count; i++)
            {
                XmlAttribute attr = node.Attributes[i];
                if (attr.Name.ToString().ToUpper() == name.ToString().ToUpper())
                    return attr.Value.ToString();
            }

            return "";
        }

        public XmlNode NodeFromRef(XmlNode node, XmlNode Ref)
        {
            if (node == null)
                return null;
            if (Ref == null)
                return null;

            return XmlNode(node, RefFromFullRef(Ref), false);
        }

        public XmlNode NodeFromRef(XmlNode node, string name, string value)
        {
            if (node == null)
                return null;

            return XmlNode(node, value, false, name);
        }

        public XmlNode XmlNode(XmlNode node, string text, bool byName = true, string byAttr = "")
        {
            // if byName = true, searching tag by name
            // if byName = false, searching tag by it's content [text] 
            // if byAttr <> "", searching tag by it's attribute [byAttr = text] 

            XmlNode found = null;
            if (node == null)
                return found;
            if (found != null)
                return found;

            foreach (XmlNode child in node.ChildNodes)
            {
                if (isCondOk(child, text, byName, byAttr))
                {
                    if (byName || byAttr != "")
                        return child;
                    else
                        return child.ParentNode;
                }

                found = XmlNode(child, text, byName, byAttr);

                if (found != null)
                    return found;
            }

            return found;
        }
    }
}