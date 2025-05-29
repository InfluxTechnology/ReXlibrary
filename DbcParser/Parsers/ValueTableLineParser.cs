using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

namespace DbcParserLib.Parsers
{
    public class ValueTableLineParser : ILineParser
    {
        private const string ValueTableLineStarter = "VAL_";
        private const string ValueTableLineRegex = @"VAL_\s+(\d+)\s+(\w+)((\s+([+-]?(?=\.\d|\d)(?:\d+)?(?:\.?\d*))(?:[Ee]([+-]?\d+))?\s+""([^""]*)"")+)\s+;";
        private const string ValueTableLineRawRegex = @"VAL_\s+\d+\s+\w+\s*(.*)\s*;";
        private const string ValueTableOnlyRegex = @"(\s*([+-]?(?=\.\d|\d)(?:\d+)?(?:\.?\d*))(?:[Ee]([+-]?\d+))?\s+""([^""]*)"")+";

        private const string ValueTableLineLinkRegex = @"VAL_\s+(\d+)\s+(\w+)\s+(\w+)\s*;";

        private const string ValueNamedTableLineRegex = @"VAL_TABLE_\s+(\w+)(.*)\s*;";

        public bool TryParse(string line, IDbcBuilder builder)
        {
            var ci = new CultureInfo("en-US", false);

            if (line.TrimStart().StartsWith(ValueTableLineStarter) == false)
                return false;

            Match match;
            if (line.TrimStart().StartsWith("VAL_TABLE_ "))
            {
                match = Regex.Match(line, ValueNamedTableLineRegex);
                if (match.Success)
                    builder.AddNamedValueTable(match.Groups[1].Value, match.Groups[2].Value);

                return true;
            }

            Signal sg = null;
            match = Regex.Match(line, ValueTableLineLinkRegex);
            if (match.Success)
            {
                if (uint.TryParse(match.Groups[1].Value, out UInt32 ident))
                {
                    if ((sg = builder.GetSignal(ident, match.Groups[2].Value)) != null)
                        builder.LinkNamedTableToSignal(ident, match.Groups[2].Value, match.Groups[3].Value);
                }
                else
                    return false;
            }
            else
            {
                match = Regex.Match(line, ValueTableLineRegex);
                if (match.Success)
                {
                    if (uint.TryParse(match.Groups[1].Value, out UInt32 ident))
                        if ((sg = builder.GetSignal(ident, match.Groups[2].Value)) != null)
                        {
                            match = Regex.Match(line, ValueTableLineRawRegex);
                            if (match.Success)
                                sg.ValueTable = match.Groups[1].Value;
                            else
                                return false;
                        }
                }
            }

            if (sg != null && sg.ValueTable is not null && sg.ValueTable != string.Empty)
            {
                match = Regex.Match(sg.ValueTable, ValueTableOnlyRegex);
                if (match.Success)
                {
                    sg.TableVerbal = new SortedDictionary<double, string>();

                    for (int i = 0; i < match.Groups[2].Captures.Count; i++)
                        if (double.TryParse(match.Groups[2].Captures[i].Value, NumberStyles.None, ci, out double InVal))
                            sg.TableVerbal.Add(InVal, match.Groups[4].Captures[i].Value);
                        else
                        {
                            sg.TableVerbal = null;
                            return false;
                        }

                    // Try to make numeric table
                    sg.TableNumeric = new SortedDictionary<double, double>();
                    foreach (var pair in sg.TableVerbal)
                        if (double.TryParse(pair.Value, NumberStyles.Float, ci, out double OutVal))
                            sg.TableNumeric.Add(pair.Key, OutVal);
                        else
                        {
                            sg.TableVerbal = null;
                            break;
                        }

                    return true;
                }
            }

            return false;
        }
    }
}