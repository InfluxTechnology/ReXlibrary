using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace InfluxShared.FileObjects
{
    internal class KvaserExporter
    {
        public static string FormatLogLine(OutputMessage msg, double timeSeconds, int counter)
        {
            var sb = new StringBuilder();

            sb.AppendFormat(CultureInfo.InvariantCulture,
                "{0,10:F6}  {1,2}  {2,8:X}  {3,-2}  {4,2}  ",
                timeSeconds,
                1,
                msg.CanID,
                "Tx",
                msg.DLC);

            // Data bytes
            for (int i = 0; i < msg.DLC; i++)
            {
                sb.AppendFormat("{0:X2} ", msg.Data[i]);
            }

            // Padding if DLC < 8
            for (int i = msg.DLC; i < 8; i++)
            {
                sb.Append("   ");
            }

            sb.AppendFormat("{0,8}", counter);

            return sb.ToString();
        }

        public static void WriteLogFile(string path, List<OutputMessage> messages)
        {
            using var writer = new StreamWriter(path, false, Encoding.ASCII);

            // Header
            writer.WriteLine("                              Kvaser Memorator Log");
            writer.WriteLine("                              ====================");
            writer.WriteLine();
            writer.WriteLine($"Memorator Binary logfile created at: {DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")}");
            writer.WriteLine();
            writer.WriteLine();

            writer.WriteLine("Settings:");
            writer.WriteLine("   Format of data field: HEX");
            writer.WriteLine("   Format of id field:   HEX");
            writer.WriteLine("   Timestamp Offset:     0.000000           s");
            writer.WriteLine("   CAN channel:          1 2 3 4 5");
            writer.WriteLine();
            writer.WriteLine("        Time Chan   Identifier Flags        DLC  Data                           Counter");
            writer.WriteLine("====================================================================================");

            for (int i = 0; i < messages.Count; i++)
            {
                writer.WriteLine(FormatLogLine(messages[i], (double)(i + 1) / 100 + 1, i+1));
            } 
        }
    }
}
