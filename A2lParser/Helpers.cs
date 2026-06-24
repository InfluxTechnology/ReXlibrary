using A2lParserLib.Settings;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using static A2lParserLib.Enums;

namespace A2lParserLib
{
    static class Helpers
    {
        public static IEnumerable<string> GetFirstInstanceTextBetween(string source, string leftWord, string rightWord, string customRegex = "")
        {
            var strFilt = String.Format(@"(?<={0})(.*?)(?={1})", leftWord, rightWord);
            if (customRegex != "")
            {
                strFilt = customRegex;
            }
            var matches = Regex.Matches(source, strFilt,
                RegexOptions.IgnoreCase | RegexOptions.Singleline);
            foreach (Match item in matches)
            {
                yield return item.Value;
            }
        }

        public static string GetStringAfter(string source, string leftWord)
        {
            //var strFilt = String.Format(@"(?<={0})(.*?)(?=\n)", leftWord);
           /* var strFilt = $@"(?<={leftWord}\s*)(.*?)(?=\n)";
            var matches = Regex.Match(source, strFilt,
                RegexOptions.IgnoreCase | RegexOptions.Singleline);
            return matches.Value.Trim();*/

            var strFilt = $@"{leftWord}\s*(.*?)\s*(?=\n)";

            // Perform the regex match
            var matches = Regex.Match(source, strFilt, RegexOptions.IgnoreCase | RegexOptions.Singleline);

            // Return the matched value, trimmed of extra spaces
            return matches.Groups[1].Value.Trim();

        }

        public static string GetTextAfter(string source, string pattern)
        {
            var matches = Regex.Matches(source, pattern, RegexOptions.IgnoreCase | RegexOptions.Singleline);
            if (matches.Count > 0 && source.Length > matches[0].Index + pattern.Length)
                return source.Substring(matches[0].Index + pattern.Length);
            return "";
        }


        public static string Cleanup(this string value)
        {
            return Regex.Replace(value, @"/\*.*?\*/\s*", "").Trim();
            //return value.Trim().Replace("\"", string.Empty);
        }

        public static T ToEnum<T>(this string value, bool ignoreCase = true)
        {
            return (T)Enum.Parse(typeof(T), value, ignoreCase);
        }

        public static T ToEnumTry<T>(this string value, bool ignoreCase = true)
        {
            try
            {
                return (T)Enum.Parse(typeof(T), value, ignoreCase);
            }
            catch (Exception)
            {
                return default(T);
            }
        }

        public static T StrToIntDef<T>(string value, T defValue) where T : IConvertible
        {
            if (string.IsNullOrWhiteSpace(value))
                return defValue;

            try
            {
                return
                    value.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ?
                    (T)Convert.ChangeType(Convert.ToUInt64(value.Substring(2), 16), typeof(T)) :
                    (T)Convert.ChangeType(value, typeof(T));
            }
            catch
            {
                return defValue;
            }
        }

        public static uint StrToIntDef(string value) 
        {

            if (value.ToUpper().StartsWith("0X"))
            {
                return Convert.ToUInt32(value.Substring(2), 16);
            }
            else if (value == "")
                return 0;
            else
            {
                return Convert.ToUInt32(value);
            }
        }

        public static int StrToIntDef(string value, int defValue)
        {
            if (value.ToUpper().StartsWith("0X"))
            {
                return Convert.ToInt32(value.Substring(2), 16);
            }
            else if (value == "")
                return defValue;
            else
            {
                return Convert.ToInt32(value);
            }
        }

        public static List<string> ToList(this MatchCollection matchCollection)
        {
            List<string> res = new();
            foreach (Match match in matchCollection)
            {
                for (int i = 1; i < match.Groups.Count; i++)
                {
                    res.Add(match.Groups[i].Value);
                }
            }
            return res;
        }

        public static ulong GetPrescale(this XcpEvent ccp_xcp_event, bool isCcp = false)
        {
            if (!isCcp)
                return (uint)Math.Pow(10, ccp_xcp_event.TimeUnit) * ccp_xcp_event.TimeCycle;
            else
                return GetPrescaleCcp(ccp_xcp_event.TimeUnit, ccp_xcp_event.TimeCycle);
        }

        public static ulong GetPrescale(this XcpDaq ccp_daq, uint scalingUnit, ulong rate)
        {
            return GetPrescaleCcp(scalingUnit, rate);
        }

        public static string GetPrescaleString(this XcpEvent ccp_xcp_event, bool isCcp = false)
        {
            if (isCcp)
            {
                return GetPrescaleStringCcp((ushort)ccp_xcp_event.TimeUnit, ccp_xcp_event.TimeCycle);
            }
            else
            {
                ulong rateInNanosec = 1 * (uint)Math.Pow(10, ccp_xcp_event.TimeUnit) * ccp_xcp_event.TimeCycle;
                if (rateInNanosec == 0)
                {
                    return $"Synch Channel {ccp_xcp_event.Channel + 1}";
                }
                else
                {
                    if (rateInNanosec >= 1_000_000_000)
                        return $"{(double)rateInNanosec / 1000000000} sec"; // Seconds
                    else if (rateInNanosec >= 1_000_000)
                        return $"{(double)rateInNanosec / 1000000} msec"; // Milliseconds
                    else if (rateInNanosec >= 1000)
                        return $"{(double)rateInNanosec / 1000} microsec"; // Microseconds
                    else
                        return $"{(double)rateInNanosec} nanosec"; // Nanoseconds
                }
            }
        }

        private static ulong GetPrescaleCcp(uint scalingUnit, ulong rate)
        {
            if (scalingUnit <= 7)
                return (uint)Math.Pow(10, scalingUnit) * rate;
            return 0;
        }

        private static string GetPrescaleStringCcp(ushort scalingUnit, ulong rate)
        {
            ulong rateInMicrosec = 100000;
            if (scalingUnit <= 7)
            {
                rateInMicrosec = (uint)Math.Pow(10, scalingUnit) * rate;
                if (rateInMicrosec >= 1_000_000)
                    return $"{(double)rateInMicrosec / 1000000} sec"; // Seconds
                else if (rateInMicrosec >= 1000)
                    return $"{(double)rateInMicrosec / 1000} msec"; // Milliseconds
                else
                    return $"{(double)rateInMicrosec} microsec"; // Microseconds
            }
            else if (scalingUnit == 8)
                return $"{(double)rate} min"; // minutes
            else if (scalingUnit == 9)
                return $"{(double)rate} hour"; // hours
            else if (scalingUnit == 10)
                return $"{(double)rate} day"; // days
            else if (scalingUnit == 100)
                return $"{(double)rate} deg"; // Angular degrees
            else if (scalingUnit == 101)
                return $"{(double)rate} rev"; // Revolution 360 degrees
            else if (scalingUnit == 102)
                return $"{(double)rate} cycle"; // Cycle 720 degrees
            else if (scalingUnit == 103)
                return $"Synch Channel {(double)rate}"; // Cylinder segment
            else if (scalingUnit == 998)
                return $"Frame {(double)rate}"; // When frame available
            else if (scalingUnit == 999)
                return $"On New Value"; // Always if there's new value
            else if (scalingUnit == 1000)
                return $"Non deterministic"; //Without fixed scaling

            return "100 msec";
        }
    }
}
