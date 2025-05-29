using InfluxShared.FileObjects;
using System;
using System.Globalization;
using System.Text.RegularExpressions;

namespace DbcParserLib.Parsers
{
    public class SignalLineParser : ILineParser
    {
        private delegate void ParsingStrategy(string line, IDbcBuilder builder);

        private const string SignalLineStarter = "SG_";
        private const string SignedSymbol = "-";
        private static readonly string[] m_commaSpaceSeparator = new string[] { Helpers.Space, Helpers.Comma };
        //private const string SignalRegex = @"\s*SG_\s+([A-Za-z0-9()_]+)\s*([Mm\d]*)\s*:\s*(\d+)\|(\d+)@([01])([+-])\s+\(([\d\+\-eE.]+),([\d\+\-eE.]+)\)\s+\[([\d\+\-eE.]+)\|([\d\+\-eE.]+)\]\s+""(.*)""\s+([\w\s,]+)";
        private const string SignalRegex = @"\s*SG_\s+([A-Za-z0-9()_]+)\s*([Mm\d]*)\s*:\s*(\d+)\s*\|\s*(\d+)@([01])([+-])\s*\(([\d\+\-eE.]+),([\d\+\-eE.]+)\)\s+\[([\d\+\-eE.]+)\|([\d\+\-eE.]+)\]\s*""(.*)""\s+([\w\s,]+)";
        private const string SignalValTypeStarter = "SIG_VALTYPE_";
        private const string SignalValTypeRegex = @"\s*SIG_VALTYPE_\s+(\d+)\s*([A-Za-z0-9()_]+)\s*:\s*(\d+)";

        public SignalLineParser()
            : this(true)
        { }

        public SignalLineParser(bool withRegex)
        {
        }

        public bool TryParse(string line, IDbcBuilder builder)
        {
            if (line.TrimStart().StartsWith(SignalLineStarter))
                AddSignal(line, builder);
            else if (line.TrimStart().StartsWith(SignalValTypeStarter))
                UpdateSignalValueType(line, builder);
            else
                return false;

            return true;
        }

        private static void UpdateSignalValueType(string line, IDbcBuilder builder)
        {
            var match = Regex.Match(line, SignalValTypeRegex);

            if (match.Success == false)
                return;

            Signal sg;
            if (int.TryParse(match.Groups[3].Value, out int sgType) && (sgType == 1 || sgType == 2))
                if (uint.TryParse(match.Groups[1].Value, out UInt32 ident))
                    if ((sg = builder.GetSignal(ident, match.Groups[2].Value)) != null)
                        sg.ValueType = sgType == 1 ? DBCValueType.IEEEFloat : DBCValueType.IEEEDouble;
        }

        /// <summary>
        /// This method parses using Regex instead of string split. Beside benchmarking (which may not be the main reason), 
        /// I find that this allows much better control over syntax, create way less arrays and strings, is more robust over spaces etc
        /// </summary>
        /// <param name="line">The line to be parsed</param>
        /// <param name="builder">The dbc builder to be used</param>
        private static void AddSignal(string line, IDbcBuilder builder)
        {
            var match = Regex.Match(line, SignalRegex);

            if (match.Success == false)
                return;

            var ci = new CultureInfo("en-US", false);

            var sig = new Signal
            {
                Multiplexing = match.Groups[2].Value,
                Name = match.Groups[1].Value,
                StartBit = ushort.Parse(match.Groups[3].Value, ci),
                //Length = byte.Parse(match.Groups[4].Value, ci),
                ByteOrder = byte.Parse(match.Groups[5].Value, ci),   // 0 = MSB (Motorola), 1 = LSB (Intel)
                ValueType = match.Groups[6].Value == SignedSymbol ? DBCValueType.Signed : DBCValueType.Unsigned,
                Factor = double.Parse(match.Groups[7].Value, ci),
                Offset = double.Parse(match.Groups[8].Value, ci),
                Minimum = double.Parse(match.Groups[9].Value, ci),
                Maximum = double.Parse(match.Groups[10].Value, ci),
                Unit = match.Groups[11].Value,
                Receiver = match.Groups[12].Value.Split(m_commaSpaceSeparator, StringSplitOptions.RemoveEmptyEntries)  // can be multiple receivers splitted by ","
            };


            if (byte.TryParse(match.Groups[4].Value, NumberStyles.None, ci, out byte length) && length >= 0 && length <= 255)
                sig.Length = length;
            else
                sig.Length = 8;

            builder.AddSignal(sig);
        }
    }
}