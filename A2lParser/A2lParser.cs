using A2lParserLib.CompuMethods;
using A2lParserLib.Interfaces;
using A2lParserLib.Items;
using A2lParserLib.Settings;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using static A2lParserLib.Enums;
using static A2lParserLib.Helpers;

namespace A2lParserLib
{
    public class A2lParser
    {
        private const string M_ST_START = @"/begin(\s*)MEASUREMENT";
        private const string M_ST_END = @"/end(\s*)MEASUREMENT";
        private const string C_ST_START = @"/begin(\s*)CHARACTERISTIC";
        private const string C_ST_END = @"/end(\s*)CHARACTERISTIC";
        private const string CM_ST_START = @"/begin(\s*)COMPU_METHOD";
        private const string CM_ST_END = @"/end(\s*)COMPU_METHOD";
        private const string CMV_ST_START = @"/begin(\s*)COMPU_VTAB";
        private const string CMV_ST_END = @"/end(\s*)COMPU_VTAB";
        private const string XCP_START = @"/begin(\s*)IF_DATA(\s*)XCP";
        private const string XCP_END = @"/end(\s*)IF_DATA";
        private const string XCP_START_PLUS = @"/begin(\s*)IF_DATA(\s*)XCPplus";
        private const string XCP_END_PLUS = @"/end(\s*)IF_DATA";
        private const string DAQ_LIST_START = @"/begin(\s*)DAQ_LIST\b";
        private const string DAQ_LIST_END = @"/end(\s*)DAQ_LIST\b";
        private const string EVENT_START = @"/begin(\s*)EVENT\b";
        private const string EVENT_END = @"/end(\s*)EVENT\b";
        private const string XCP_ON_CAN_START = @"/begin(\s*)XCP_ON_CAN\b";
        private const string XCP_ON_CAN_END = @"/end(\s*)XCP_ON_CAN\b";
        private const string DAQ_LIST_CAN_ID_START = @"/begin(\s*)DAQ_LIST_CAN_ID\b";
        private const string DAQ_LIST_CAN_ID_END = @"/end(\s*)DAQ_LIST_CAN_ID\b";
        private const string CCP_START = @"/begin(\s*)IF_DATA(\s*)ASAP1B_CCP";
        private const string CCP_END = @"/end(\s*)IF_DATA";
        private const string CCP_SOURCE_START = @"/begin(\s*)SOURCE\b";
        private const string CCP_SOURCE_END = @"/end(\s*)SOURCE\b";
        private const string CCP_RASTER_START = @"/begin(\s*)RASTER\b";
        private const string CCP_RASTER_END = @"/end(\s*)RASTER\b";
        private const string TP_BLOB_START = @"/begin(\s*)TP_BLOB";
        private const string TP_BLOB_END = @"/end(\s*)TP_BLOB";

        public string FileName { get; set; }
        public string FileNameSerialized { get; set; }
        public Module Module { get; private set; }
        public async Task<Module> LoadA2l(string filePath, Action<object> ProgressCallback = null)
        {
            var data = System.IO.File.ReadAllText(filePath, Encoding.UTF8);
            Module = new Module();
            Module.XcpSettingsList = await GetXcpSettings(data);
            Module.XcpSettingsList.AddRange(await GetCcpSettings(data));
            for (int i = Module.XcpSettingsList.Count - 1; i >= 0; i--)
            {
                if (Module.XcpSettingsList[i].Events.Count == 0 && Module.XcpSettingsList[i].Daqs.Count == 0)
                    Module.XcpSettingsList.Remove(Module.XcpSettingsList[i]);
            }
            if (Module.XcpSettingsList.Count == 0)
                Module.XcpSettingsList.Add(new XcpSettings());
            Module.XcpSettings = Module.XcpSettingsList[0];
            ProgressCallback?.Invoke("Reading CompuMethods...");
            ProgressCallback?.Invoke(33);
            Module.CompuMethods = await GetCompuMethods(data);
            ProgressCallback?.Invoke(66);
            ProgressCallback?.Invoke("Reading Measurements...");
            Module.Measurements = await GetMeasurements(data);
            ProgressCallback?.Invoke("Reading Characteristics...");
            ProgressCallback?.Invoke(98);
            Module.Characteristics = await GetCharacteristics(data);
            ProgressCallback?.Invoke(100);
            
            return Module;
        }

        async Task<List<Measurement>> GetMeasurements(string data)
        {
            List<Measurement> measurements = new List<Measurement>();
            string pattern = @"([\S]+)\s+""([^""]+)""\s+(\S+)\s+(\S+)\s+(\d+)\s+(\d+)\s+(-?\d+\.?\d*)\s+(-?\d+\.?\d*)";
            pattern = @"([\S]+)\s+""([^""]*)""\s+(\S+)\s+(\S+)\s+(\d+)\s+(\d+)\s+(-?\d+\.?\d*)\s+(-?\d+\.?\d*)";
            pattern = @"(\S+)+\s+(\S+)+\s+(\d)+\s+([+-]?(?=\.\d|\d)(?:\d+)?(?:\.?\d*))(?:[Ee]([+-]?\d+))?\s+([+-]?(?=\.\d|\d)(?:\d+)?(?:\.?\d*))(?:[Ee]([+-]?\d+))?\s+([+-]?(?=\.\d|\d)(?:\d+)?(?:\.?\d*))(?:[Ee]([+-]?\d+))?";
            string namePattern = @"^(\S+)+\s+(.*)";

            foreach (string measurementStr in GetFirstInstanceTextBetween(data, M_ST_START, M_ST_END))
            {
                if (!measurementStr.ToLower().Contains("ecu_address"))
                    continue;
                string cleanedInput = Regex.Replace(measurementStr, @"/\*.*?\*/", "").Trim();  //remove comments
                cleanedInput = cleanedInput.Replace("\\", "").Replace("\"\"", "\"");
                //Match match = Regex.Match(cleanedInput, pattern);
                Measurement measVar = null;
                try
                {
                    measVar = new Measurement();
                    Match nameRegex = Regex.Match(cleanedInput, namePattern);
                    measVar.Name = nameRegex.Groups[1].Value.Trim();
                    measVar.Description = nameRegex.Groups[2].Value.Cleanup();
                    Match match = Regex.Match(cleanedInput.Substring(nameRegex.Length + 1), pattern);
                    measVar.DataType = match.Groups[1].Value.Cleanup().ToEnum<DataType>();
                    measVar.Size = measVar.DataType.ToSize();
                    if (cleanedInput.Contains("BYTE_ORDER"))
                    {
                        if (GetStringAfter(cleanedInput, "BYTE_ORDER").Contains("MSB_FIRST"))
                            measVar.ByteOrder = ByteOrder.Motorola;
                        else
                            measVar.ByteOrder = ByteOrder.Intel;
                    }
                    else
                    {
                        if (Module.XcpSettings != null)
                            measVar.ByteOrder = Module.XcpSettings.ByteOrder;
                    }
                    if (Module.CompuMethods.TryGetValue(match.Groups[2].Value.Cleanup(), out CompuMethod compuMethod))
                    {
                        measVar.CompuMethod = compuMethod;
                        measVar.Units = compuMethod.Units;
                    }
                    //measVar.DefinedResolution = double.Parse(strSplt[4].Cleanup());
                    //measVar.DefinedAccuracy_Prc = double.Parse(strSplt[5].Cleanup());
                    measVar.MinValue = double.Parse(match.Groups[6].Value.Cleanup());
                    measVar.MaxValue = double.Parse(match.Groups[8].Value.Cleanup());
                    measVar.EcuAddress = StrToIntDef(GetStringAfter(measurementStr, "ECU_ADDRESS").Cleanup());
                    var arraySize = StrToIntDef(GetStringAfter(measurementStr, "ARRAY_SIZE").Cleanup());
                    string matrixDim = GetStringAfter(measurementStr, "MATRIX_DIM").Cleanup();
                    if (matrixDim != "")
                        arraySize = GetMatrixSize(matrixDim);

                    if (arraySize > 0)
                    {
                        string origName = measVar.Name;
                        uint origAddress = measVar.EcuAddress;
                        measVar.Name = origName + "_[0]";
                        measurements.Add(measVar);
                        for (uint i = 1; i < arraySize; i++)
                        {
                            var newMeasVar = measVar.ShallowCopy();
                            newMeasVar.Name = $"{origName}_[{i}]";
                            newMeasVar.EcuAddress = origAddress + i * measVar.DataType.ToSize();
                            measurements.Add(newMeasVar);
                        }
                    }
                    else
                        measurements.Add(measVar);

                    //measVar.CustomDisplayIdentifier = GetStringAfter(measurementStr, "DISPLAY_IDENTIFIER ").Cleanup();
                    //TODO: Functions References are not fetched - get reference
                }
                catch (Exception exc)
                {
                    Module.ErrorLog.AddError($"{measVar.Name} : {exc.Message}");
                }
            }
            return measurements;
        }

        private uint GetMatrixSize(string matrixDim)
        {
            var dimensions = matrixDim.Split(' ');
            uint size = 1;
            foreach (var dimension in dimensions)
            {
                try
                {
                    size *= uint.Parse(dimension);
                }
                catch (Exception)
                {
                    
                }
            }
            return size;
        }

        async Task<List<Characteristic>> GetCharacteristics(string data)
        {
            List<Characteristic> characteristics = new();
            string pattern = @"(\S+)+\s+(\S+)+\s+(\S+)+\s+([+-]?(?=\.\d|\d)(?:\d+)?(?:\.?\d*))(?:[Ee]([+-]?\d+))?\s+(\S+)+\s+([+-]?(?=\.\d|\d)(?:\d+)?(?:\.?\d*))(?:[Ee]([+-]?\d+))?\s+([+-]?(?=\.\d|\d)(?:\d+)?(?:\.?\d*))(?:[Ee]([+-]?\d+))?";
            string namePattern = @"^(\S+)+\s+(.*)";
            foreach (string calibrationStr in GetFirstInstanceTextBetween(data, C_ST_START, C_ST_END))
            {                
                string cleanedInput = Regex.Replace(calibrationStr, @"/\*.*?\*/", "").Trim();  //remove comments
                //Match match = Regex.Match(cleanedInput, pattern);
                Characteristic item = null;
                try
                {
                    item = new Characteristic();
                    Match nameRegex = Regex.Match(cleanedInput, namePattern);
                    item.Name = nameRegex.Groups[1].Value.Cleanup();
                    item.Description = nameRegex.Groups[2].Value.Cleanup();
                    string characteristicPattern = @"^(\S+)\s+(\S+)\s+(\S+)\s+(\S+)\s+(\S+)\s+(\S+)\s+(\S+)";

                    //Match match = Regex.Match(cleanedInput.Substring(nameRegex.Length + 1).Cleanup(), characteristicPattern);
                    var values = cleanedInput.Cleanup().Split((char[])null, StringSplitOptions.RemoveEmptyEntries).ToList();
                    var idx = values.IndexOf("VALUE");
                    if (idx > 0)
                    {
                        item.EcuAddress = StrToIntDef(values[idx + 1]);
                        item.DataType = values[idx + 2].ToDataType();
                        item.Size = item.DataType.ToSize();
                        if (cleanedInput.Contains("BYTE_ORDER"))
                        {
                            if (GetStringAfter(cleanedInput, "BYTE_ORDER").Contains("MSB_FIRST"))
                                item.ByteOrder = ByteOrder.Motorola;
                            else
                                item.ByteOrder = ByteOrder.Intel;
                        }
                        else
                        {
                            if (Module.XcpSettings != null)
                                item.ByteOrder = Module.XcpSettings.ByteOrder;
                        }
                        if (Module.CompuMethods.TryGetValue(values[idx + 4], out CompuMethod compuMethod))
                        {
                            item.CompuMethod = compuMethod;
                            item.Units = compuMethod.Units;
                        }
                        item.MinValue = double.Parse(values[idx + 5].Cleanup());
                        item.MaxValue = double.Parse(values[idx + 6].Cleanup());
                        characteristics.Add(item);
                    }
                }
                catch (Exception exc)
                {
                    Module.ErrorLog.AddError($"{item.Name} : {exc.Message}");
                }


            }
            return characteristics;
        }

        async Task<Dictionary<string, CompuMethod>> GetCompuMethods(string data)
        {
            Dictionary<string, CompuTable> CompuTables = await GetCompuMethodVTabs(data);
            Dictionary<string, CompuMethod> CompuMethods = new Dictionary<string, CompuMethod>();            
            foreach (var table in CompuTables)
            {
                CompuMethods.Add(table.Key, table.Value);
            }
            //string pattern = @"(\S+)+\s+""([^""]*)""+\s+(\S+)+\s+""([^""]*)""+\s+""([^""]*)""";
            string pattern = @"^(\S+)\s+(\S+)\s+(\S+)\s+(\S+)\s+(\S+)\s+(\S+)\s+(\S+)";

            foreach (string compuMethodStr in GetFirstInstanceTextBetween(data, CM_ST_START, CM_ST_END))
            {
                string cleanedInput = Regex.Replace(compuMethodStr, @"/\*.*?\*/", "").Trim();  //remove comments
                //var lines = cleanedInput.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).Select(l => l.Trim()).ToList();
                var matches = Regex.Matches(cleanedInput, "\"([^\"\\\\]*(\\\\.[^\"\\\\]*)*)\"|\\S+");  //If it's on the same row
                List<string> lines = new ();
                //if (matches.Count > 1)
                foreach (Match match in matches) 
                {
                    lines.Add(match.Value);
                   /* lines.RemoveAt(0);
                    for (int i = matches.Count - 1; i >= 0; i--)
                    {
                        lines.Insert(0, matches[i].Value.Cleanup());
                    }*/
                }
                try
                {
                    if (!CompuTables.ContainsKey(lines[0].Cleanup()))
                    {
                        CompuMethod compMet = null;
                        CompuMethodType compuType = lines[2].Cleanup().ToEnum<CompuMethodType>();
                        if (compuType == CompuMethodType.RAT_FUNC)
                        {
                            compMet = new RatFunc();
                        }
                        else if (compuType == CompuMethodType.TAB_VERB)
                        {
                            compMet = new CompuTable();
                        }
                        else if (compuType == CompuMethodType.LINEAR)
                        {
                            compMet = new Linear();
                        }
                        else
                          if (compMet is null)
                            continue;
                        compMet.Name = lines[0].Cleanup();
                        compMet.Description = lines[1].Cleanup();
                        compMet.Type = lines[2].Cleanup().ToEnum<CompuMethodType>();
                        compMet.FormatString = lines[3].Cleanup();
                        compMet.Units = lines[4].Cleanup();
                        //if (compMet.Type == CompuMethodType.TAB_VERB)
                        //    (compMet as CompuTable).RefTable = GetStringAfter(compuMethodStr, "COMPU_TAB_REF").Cleanup();
                        if (compMet.Type == CompuMethodType.RAT_FUNC)
                            GetCoeffs(compMet as RatFunc, GetStringAfter(compuMethodStr, "COEFFS").Cleanup());
                        else if (compMet.Type == CompuMethodType.LINEAR)
                            GetCoeffs(compMet as Linear, GetStringAfter(compuMethodStr, "COEFFS_LINEAR").Cleanup());

                        /*compMet.DisplayFormat = strSplt[3].Cleanup();
                        compMet.Unit = strSplt[4].Cleanup();
                        var coeffStr = GetStringAfter(compuMethodStr, "COEFFS").Cleanup().Split(' ');
                        if (coeffStr.Length > 1)
                            compMet.Coefficients = Array.ConvertAll<string, double>(coeffStr, item => double.Parse(item));*/
                        CompuMethods.Add(compMet.Name, compMet);
                    }
                }
                catch (Exception exc)
                {
                    Module.ErrorLog.AddError(exc.Message + $"  {string.Join("", compuMethodStr)}");
                }


            }
            return CompuMethods;
        }

        private void GetCoeffs(RatFunc compMet, string v)
        {
            while (v.Contains("  "))
            {
                v = v.Replace("  ", " ");
            }
            string[] coeffs = v.Split(' ');
            if (coeffs.Length < 6)
            {
                Module.ErrorLog.AddError($"Item {compMet.Name} has missing Coefficients");
                return;
            }
            else
                try
                {
                    compMet.Coeffs[1] = Convert.ToDouble(coeffs[5]) / Convert.ToDouble(coeffs[1]);
                    compMet.Coeffs[2] = Convert.ToDouble(coeffs[2]) / Convert.ToDouble(coeffs[1]) * (-1); //In Dialog it's * -1
                    compMet.Coeffs[4] = Convert.ToDouble(coeffs[4]) * (-1); //In Dialog it's * -1
                    compMet.Coeffs[5] = Convert.ToDouble(coeffs[1]);
                }
                catch (Exception)
                {
                    compMet.Coeffs[1] = 1;
                    compMet.Coeffs[2] = 0;
                    compMet.Coeffs[4] = 0;
                    compMet.Coeffs[5] = 1;
                }
        }

        private void GetCoeffs(Linear linearCompMethod, string v)
        {
            while (v.Contains("  "))
            {
                v = v.Replace("  ", " ");
            }
            string[] coeffs = v.Split(' ');
            if (coeffs.Length < 2)
            {
                Module.ErrorLog.AddError($"Item {linearCompMethod.Name} has missing Coefficients");
                return;
            }
            else
                try
                {
                    linearCompMethod.Factor = Convert.ToDouble(coeffs[0]);
                    linearCompMethod.Offset = Convert.ToDouble(coeffs[1]);

                }
                catch (Exception)
                {
                    linearCompMethod.Factor = 1;
                    linearCompMethod.Offset = 0;
                }
        }

        private void GetTableValues(CompuMethod compMet, uint size, string compuMethodStr)
        {
            
        }

        async Task<Dictionary<string, CompuTable>> GetCompuMethodVTabs(string data)
        {
            Dictionary<string, CompuTable> CompuMethodVTabs = new();
            foreach (string compuMethodVTabStr in GetFirstInstanceTextBetween(data, CMV_ST_START, CMV_ST_END))
            {
                string cleanedInput = Regex.Replace(compuMethodVTabStr, @"/\*.*?\*/", "").Trim();  //remove comments

                //var lines = cleanedInput.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).Select(l => l.Trim()).ToList();
                var matches = Regex.Matches(cleanedInput.Cleanup(), "\"([^\"\\\\]*(\\\\.[^\"\\\\]*)*)\"|\\S+");
               
                try
                {
                    if (!CompuMethodVTabs.ContainsKey(matches[0].Value.Cleanup()))
                    {
                        CompuTable table = new CompuTable();
                        table.Name = matches[0].Value.Cleanup();
                        table.Description = matches[1].Value.Cleanup();
                        table.Type = matches[2].Value.Cleanup().ToEnum<CompuMethodType>();
                        table.NumberOfPairs = int.Parse(matches[3].Value.Cleanup());
                        table.Values = new Dictionary<Int64, object>();
                        for (int i = 0; i < table.NumberOfPairs; i++)
                        {
                            if (matches.Count > 5 + i * 2)
                                try
                                {
                                    string line = matches[4 + i].Value;
                                    /* int sep = line.IndexOfAny(new[] { ' ', '\t' });
                                     if (sep < 0)
                                         throw new FormatException($"Invalid key/value line: {line}");*/
                                    Int64 key = Int64.Parse(matches[4 + i * 2].Value.Cleanup());
                                    string value = matches[5 + i * 2].Value;
                                    table.Values.Add(key, value);
                                }
                                catch (Exception exc)
                                {
                                    Module.ErrorLog.AddError($"Error parsing key:{matches[4 + i * 2].Value}");
                                    Module.ErrorLog.AddError(exc.Message + $"  Table: {table.Name}");
                                }
                        }
                        CompuMethodVTabs.Add(table.Name, table);
                    }
                }
                catch (Exception exc)
                {
                    Module.ErrorLog.AddError(exc.Message + $"  {string.Join("", compuMethodVTabStr)}");
                }

                /*var lines = cleanedInput.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).Select(l => l.Trim()).ToList();
                var matches = Regex.Matches(lines[0].Cleanup(), "\"([^\"\\\\]*(\\\\.[^\"\\\\]*)*)\"|\\S+");

                if (matches.Count > 1)
                {
                    lines.RemoveAt(0);
                    for (int i = matches.Count - 1; i >= 0; i--)
                    {
                        lines.Insert(0, matches[i].Value.Cleanup());
                    }
                }
                try
                {
                    if (!CompuMethodVTabs.ContainsKey(lines[0].Cleanup()))
                    {
                        CompuTable table = new CompuTable();
                        table.Name = lines[0].Cleanup();
                        table.Description = lines[1].Cleanup();
                        table.Type = lines[2].Cleanup().ToEnum<CompuMethodType>();
                        table.NumberOfPairs = int.Parse(lines[3].Cleanup());
                        table.Values = new Dictionary<Int64, object>();
                        for (int i = 0; i < table.NumberOfPairs; i++)
                        {
                            string line = lines[4 + i];
                            int sep = line.IndexOfAny(new[] { ' ', '\t' });
                            if (sep < 0)
                                throw new FormatException($"Invalid key/value line: {line}");
                            Int64 key = Int64.Parse(line.Substring(0, sep));
                            string value = line.Substring(sep + 1).Trim();
                            table.Values.Add(key, value);
                        }
                        CompuMethodVTabs.Add(table.Name, table); 
                    }
                }
                catch (Exception exc)
                {
                    Module.ErrorLog.AddError(exc.Message + $"  {string.Join("", compuMethodVTabStr)}");
                }*/


            }
            return CompuMethodVTabs;
        }

        List<string> SplitSettings(string input, string pattern)
        {
            List<string> res = new ();
            var matches = Regex.Matches(input, pattern)
                           .Cast<Match>()
                           .Select(m => m.Index)
                           .ToList();

            var results = new List<string>();
            for (int i = 0; i < matches.Count - 1; i++)
            {
                int startIndex = matches[i];                
                int length = matches[i + 1] - startIndex;
                results.Add(input.Substring(startIndex, length).Trim());                
            }
            if (matches.Count > 0)
            {
                results.Add(input.Substring(matches[matches.Count - 1], input.Length - matches[matches.Count - 1]).Trim());
            }

            foreach (var result in results)
            {
                res.Add(result.ToString());
            }
            return res;
        }

        async Task<List<XcpSettings>> GetXcpSettings(string data)
        {
            List<XcpSettings> settingsList = new();
            foreach (string str in GetFirstInstanceTextBetween(data, XCP_START, XCP_END))
            {
                string pattern = @"/\*.*?\*/|//.*?$"; // Regex to match /* */ style comments and // comments
                string cleanedStr = Regex.Replace(str, pattern, "", RegexOptions.Singleline | RegexOptions.Multiline);
                List<string> protocolLayerList = null;
                bool foundMultipleSettings = false;
                protocolLayerList = SplitSettings(cleanedStr, @"/begin(\s*)XCP_ON_CAN");
                foreach (var xcponcan in protocolLayerList)
                {
                    var res = Regex.Matches(xcponcan, @"/begin(\s*)PROTOCOL_LAYER").ToList();
                    if (res.Count > 0)
                        foundMultipleSettings = true;
                }
                if (!foundMultipleSettings)
                    protocolLayerList = SplitSettings(cleanedStr, @"/begin(\s*)PROTOCOL_LAYER");
                foreach (var protocolLayer in protocolLayerList)
                {
                    try
                    {
                        XcpSettings settings = new();
                        settings.IsXcp = true;
                        settingsList.Add(settings);
                        cleanedStr = protocolLayer.ToString();
                        
                        settings.Cmmds = Regex.Matches(cleanedStr, @"OPTIONAL_CMD\s+(\w+)").ToList();  //Get optional commands
                        if (cleanedStr.Contains("BYTE_ORDER_MSB_FIRST"))
                            settings.ByteOrder = ByteOrder.Motorola;
                        else
                            settings.ByteOrder = ByteOrder.Intel;
                        if (protocolLayer.Contains("TIMESTAMP_SUPPORTED"))
                        {                            
                            foreach (string tmpStr in GetFirstInstanceTextBetween(protocolLayer, @"/begin(\s*)TIMESTAMP_SUPPORTED", @"/end(\s*)TIMESTAMP_SUPPORTED"))
                            {
                                var memValuesList = Regex.Matches(tmpStr, @"([""'].*?[""']|\S+)").ToList();
                                settings.Timestamp = memValuesList[1].Cleanup().ToEnumTry<XcpTimestamp>();
                                settings.TimestampResolution = memValuesList[2].Cleanup().ToEnumTry<XcpTimestampResolution>();
                                break;
                            }
                        }
                        var res = Regex.Matches(cleanedStr, @"/begin\s+DAQ\s+(\S+)\s+(\S+)\s+(\S+)\s+(\S+)").ToList();  //Get Daq type (static, dynamic)
                        if (res.Count > 3)
                        {
                            settings.DaqType = res[0].ToUpper() == "STATIC" ? DaqType.Static : DaqType.Dynamic;
                            settings.MaxDaq = StrToIntDef(res[1].Cleanup());
                            settings.MaxEventChannels = StrToIntDef(res[2].Cleanup());
                            settings.MinDaq = (byte)StrToIntDef(res[3].Cleanup());
                        }
                        string daqSection = cleanedStr;
                        var match = Regex.Match(cleanedStr, @"/end\s+DAQ(?!\w)", RegexOptions.IgnoreCase);
                        if (match.Success)
                        {
                            daqSection = daqSection.Substring(0, match.Index).Cleanup();
                        }                        
                        settings.Daqs = GetDaqLists(daqSection);
                        settings.Events = GetEvents(daqSection);
                        GetXcpOnCan(cleanedStr, settings);
                        if (settings.Daqs.Count == 0)
                        {
                            byte maxOdt = 20;
                            //Search for DAQ_MEMORY_CONSUMPTION
                            foreach (string tmpStr in GetFirstInstanceTextBetween(cleanedStr, @"/begin(\s*)DAQ_MEMORY_CONSUMPTION", @"/end(\s*)DAQ_MEMORY_CONSUMPTION"))
                            {
                                var memValuesList = Regex.Matches(tmpStr, @"([""'].*?[""']|\S+)").ToList();
                                maxOdt = (byte)StrToIntDef(memValuesList[2], 20);
                                break;
                            }
                            foreach (var xcp_event in settings.Events)
                            {
                                XcpDaq daq = new();
                                if (xcp_event.Ident > 0)
                                    daq.Ident = xcp_event.Ident;
                                else
                                    daq.Ident = settings.Dto;
                                daq.MaxOdt = maxOdt;
                                daq.MaxOdtEntries = 7;
                                daq.DaqIndex = (byte)settings.Daqs.Count;
                                settings.Daqs.Add(daq);
                            }
                        }
                        for (int i = 0; i < settings.Daqs.Count; i++)
                        {
                            var daq = settings.Daqs[i];
                            if (daq.Name == "" && settings.Events.Count > i)
                            {
                                daq.Name = settings.Events[i].Name;
                                daq.Sampling = (settings.Events[i].GetPrescale());
                                daq.SamplingStr = settings.Events[i].GetPrescaleString();
                                daq.EventChannel = settings.Events[i].Channel;
                            }
                        }
                    }
                    catch (Exception exc)
                    {
                        Module.ErrorLog.AddError("Error Parsing XCP " + exc.Message + $"  {string.Join("", str)}");
                    }
                }
                //if (str.Contains("PROTOCOL_LAYER"))
                {
                    
                }
            }
            if (settingsList.Count == 0)
            {
                settingsList.Add(new XcpSettings());
            }
            return settingsList;
        }       
              

        private void GetXcpOnCan(string data, XcpSettings settings)
        {
            var xcp_on_can_list = GetFirstInstanceTextBetween(data, XCP_ON_CAN_START, XCP_ON_CAN_END);
            if (xcp_on_can_list.Count() == 0 && data.Contains("XCP_ON_CAN")) 
            {
                string str = GetTextAfter(data, XCP_ON_CAN_START);
                ParseXcpOnCan(data, settings, str);
            }
            else foreach (string str in xcp_on_can_list)
            {
                ParseXcpOnCan(data, settings, str);
            }

            void ParseXcpOnCan(string data, XcpSettings settings, string str)
            {
                try
                {
                    settings.OdtSize = 7;
                    string pattern = @"/\*.*?\*/|//.*?$"; // Regex to match /* */ style comments and // comments
                    string cleanedStr = Regex.Replace(str, pattern, "", RegexOptions.Singleline | RegexOptions.Multiline);
                    settings.StationAddress = (ushort)StrToIntDef(Regex.Match(cleanedStr, @"^\s*(\S+)").Groups[0].Value.Cleanup());
                    settings.Cro = (uint)StrToIntDef(Regex.Match(cleanedStr, @"CAN_ID_MASTER\s+(\S+)").Groups[1].Value.Cleanup());
                    settings.Dto = (uint)StrToIntDef(Regex.Match(cleanedStr, @"CAN_ID_SLAVE\s+(\S+)")?.Groups[1].Value.Cleanup());
                    if ((settings.Cro & 0x80000000) > 0)
                    {                        
                        settings.IsExtended = true; 
                    }
                    settings.Cro = settings.Cro & 0x1FFFFFFF; // Mask to 29 bits if it is a CAN FD ID
                    settings.Dto = settings.Dto & 0x1FFFFFFF;
                    settings.Baudrate = (uint)StrToIntDef(Regex.Match(cleanedStr, @"BAUDRATE\s+(\S+)")?.Groups[1].Value.Cleanup());
                    settings.BaudrateFD = (uint)StrToIntDef(Regex.Match(cleanedStr, @"CAN_FD_DATA_TRANSFER_BAUDRATE\s+(\S+)")?.Groups[1].Value.Cleanup());
                    if (settings.BaudrateFD > 0)
                    {
                        settings.IsCanFd = true;
                        settings.OdtSize = 63;
                    }
                    if (cleanedStr.Contains("TRANSPORT_LAYER_INSTANCE"))
                        settings.Name = Regex.Match(cleanedStr, @"TRANSPORT_LAYER_INSTANCE\s+(\S+)")?.Groups[1].Value.Cleanup();

                    var daqList = Regex.Matches(cleanedStr, @"/begin DAQ_LIST_CAN_ID\s*([\s\S]*?)\s*/end DAQ_LIST_CAN_ID").ToList();
                    if (daqList.Count == 0)
                    {
                        var evList = Regex.Matches(cleanedStr, @"/begin EVENT_CAN_ID_LIST\s*([\s\S]*?)\s*/end EVENT_CAN_ID_LIST").ToList();
                        foreach (var evStr in evList)
                        {
                            uint evIdx = StrToIntDef(Regex.Match(evStr, @"(\S+)").Groups[0].Value.Cleanup());
                            var evChannel = settings.Events.FirstOrDefault(x => x.Channel == evIdx);
                            if (evChannel is not null && evStr.Contains("FIXED"))
                            {
                                evChannel.Ident = StrToIntDef(Regex.Match(evStr, @"FIXED\s+(\S+)").Groups[1].Value.Cleanup());
                            }
                        }
                    }
                    foreach (var daqStr in daqList)
                    {
                        uint daqIdx = StrToIntDef(Regex.Match(daqStr, @"(\S+)").Groups[0].Value.Cleanup());
                        var daq = settings.Daqs.FirstOrDefault(x => x.DaqIndex == daqIdx);
                        if (daq is not null && daqStr.Contains("FIXED"))
                        {
                            daq.Ident = StrToIntDef(Regex.Match(daqStr, @"FIXED\s+(\S+)").Groups[1].Value.Cleanup());
                            daq.Ident = daq.Ident & 0x1FFFFFFF;
                        }
                    }
                }
                catch (Exception exc)
                {
                    Module.ErrorLog.AddError("Error Parsing XCP_ON_CAN " + exc.Message + $"  {string.Join("", str)}");
                }
            }
        }

        List<XcpDaq> GetDaqLists(string data)
        {
            List<XcpDaq> daqList = new();
            foreach (string str in GetFirstInstanceTextBetween(data, DAQ_LIST_START, DAQ_LIST_END))
            {
                try
                {
                    string pattern = @"/\*.*?\*/|//.*?$"; // Regex to match /* */ style comments and // comments
                    string cleanedStr = Regex.Replace(str, pattern, "", RegexOptions.Singleline | RegexOptions.Multiline);
                    var daqValuesList = Regex.Matches(cleanedStr, @"([""'].*?[""']|\S+)").ToList();
                    var idx = daqValuesList.IndexOf("DAQ_LIST_TYPE");
                    if (idx >= 0 && daqValuesList.Count > idx + 1 && daqValuesList[idx + 1] != "DAQ")
                        continue;

                    XcpDaq daq = new();
                    daq.DaqIndex = (byte)StrToIntDef(daqValuesList[0]);
                    for (int i = 0; i < daqValuesList.Count; i++)
                    {
                        if (daqValuesList[i].ToUpper() == "MAX_ODT" && daqValuesList.Count > i + 1)
                            daq.MaxOdt = (byte)StrToIntDef(daqValuesList[i + 1]);
                        if (daqValuesList[i].ToUpper() == "MAX_ODT_ENTRIES" && daqValuesList.Count > i + 1)
                            daq.MaxOdtEntries = (byte)StrToIntDef(daqValuesList[i + 1]);
                        if (daqValuesList[i].ToUpper() == "FIRST_PID" && daqValuesList.Count > i + 1)
                            daq.FirstPid = (byte)StrToIntDef(daqValuesList[i + 1]);
                        if (daqValuesList[i].ToUpper() == "EVENT_FIXED" && daqValuesList.Count > i + 1)
                        {
                            daq.EventFixed = true;
                            daq.EventChannel = (byte)StrToIntDef(daqValuesList[i + 1]);
                        }
                    }           
                    daqList.Add(daq);
                }
                catch (Exception exc)
                {
                    Module.ErrorLog.AddError("Error Parsing DAQ_LIST " +exc.Message + $"  {string.Join("", str)}");
                }
            }
            
            return daqList;
        }

        List<XcpEvent> GetEvents(string data)
        {
            List<XcpEvent> events = new();
            foreach (string str in GetFirstInstanceTextBetween(data, EVENT_START,EVENT_END))
            {
                try
                {
                    string pattern = @"/\*.*?\*/|//.*?$"; // Regex to match /* */ style comments and // comments
                    string cleanedStr = Regex.Replace(str, pattern, "", RegexOptions.Singleline | RegexOptions.Multiline);
                    var evValuesList = Regex.Matches(cleanedStr, @"([""'].*?[""']|\S+)").ToList();
                    if (evValuesList.Count > 3 && evValuesList[3] != "DAQ")
                        continue;

                    XcpEvent xcpEvent = new();
                    xcpEvent.Name = evValuesList[0].Replace("\"", "");
                    xcpEvent.ShortName = evValuesList[1].Replace("\"", "");
                    xcpEvent.Channel = (byte)StrToIntDef(evValuesList[2]);
                    if (evValuesList.Count > 4)
                        xcpEvent.MaxDaqList = (byte)StrToIntDef(evValuesList[4]);
                    if (evValuesList.Count > 5)
                        xcpEvent.TimeCycle = StrToIntDef(evValuesList[5]);
                    if (evValuesList.Count > 6)
                        xcpEvent.TimeUnit = (byte)StrToIntDef(evValuesList[6]);
                    events.Add(xcpEvent);
                }
                catch (Exception exc)
                {
                    Module.ErrorLog.AddError("Error Parsing EVENT " + exc.Message + $"  {string.Join("", str)}");
                }
            }
            return events;
        }


        async Task<List<XcpSettings>> GetCcpSettings(string data)
        {
            List<XcpSettings> settingsList = new();
            foreach (string asap1b in GetFirstInstanceTextBetween(data, CCP_START, CCP_END))
            {
                string pattern = @"/\*.*?\*/|//.*?$"; // Regex to match /* */ style comments and // comments
                string cleanedStr = Regex.Replace(asap1b, pattern, "", RegexOptions.Singleline | RegexOptions.Multiline);
                XcpSettings settings = new();
                settings.IsXcp = false;
                settings.Daqs = GetCcpSource(cleanedStr);
                settings.Events = GetCcpRaster(cleanedStr);
                if (cleanedStr.Contains("TP_BLOB"))
                {
                    foreach (string tp_blop in GetFirstInstanceTextBetween(cleanedStr, TP_BLOB_START, TP_BLOB_END))
                    {
                        string tp_blop_cleaned = Regex.Replace(tp_blop, pattern, "", RegexOptions.Singleline | RegexOptions.Multiline);
                        var tp_blop_values = Regex.Matches(tp_blop_cleaned, @"([""'].*?[""']|\S+)").ToList();
                        settings.Cro = (uint)StrToIntDef(tp_blop_values[2]);
                        settings.Dto = (uint)StrToIntDef(tp_blop_values[3]);
                        settings.StationAddress = (ushort)StrToIntDef(tp_blop_values[4]);
                        if (StrToIntDef(tp_blop_values[5]) == 1)
                            settings.ByteOrder = ByteOrder.Motorola;
                        else
                            settings.ByteOrder = ByteOrder.Intel;
                        for (int i = 0; i < tp_blop_values.Count; i++)
                        {
                            if (tp_blop_values[i].ToUpper() == "BAUDRATE" && tp_blop_values.Count > i + 1)
                                settings.Baudrate = (uint)StrToIntDef(tp_blop_values[i + 1]);                            
                        }
                    }

                }
                for (int i = 0; i < settings.Daqs.Count; i++)
                {
                    var daq = settings.Daqs[i];
                    /* if (settings.Events.Count > daq.EventChannel)
                     {
                         var raster = settings.Events[daq.EventChannel];                        
                         daq.Sampling = raster.GetPrescale(true);
                         daq.SamplingStr = raster.GetPrescaleString(true);
                     }  */          //Doesn't work if EventChannels are not sequential             
                    foreach (var raster in settings.Events)
                    {
                        if (raster.Channel == daq.EventChannel)
                        {
                            daq.Sampling = raster.GetPrescale(true);
                            daq.SamplingStr = raster.GetPrescaleString(true);
                            break;
                        }
                    }
                }
                if (settings.Daqs.Count > 0)
                {
                    settingsList.Add(settings);
                }
            }
            if (settingsList.Count == 0)
            {
                settingsList.Add(new XcpSettings());
            }            
            return settingsList;
        }

        private List<XcpEvent> GetCcpRaster(string data)
        {
            List<XcpEvent> events = new();
            foreach (string str in GetFirstInstanceTextBetween(data, CCP_RASTER_START, CCP_RASTER_END))
            {
                try
                {
                    string pattern = @"/\*.*?\*/|//.*?$"; // Regex to match /* */ style comments and // comments
                    string cleanedStr = Regex.Replace(str, pattern, "", RegexOptions.Singleline | RegexOptions.Multiline);
                    var evValuesList = Regex.Matches(cleanedStr, @"([""'].*?[""']|\S+)").ToList();

                    XcpEvent raster = new();
                    raster.Name = evValuesList[0].Replace("\"", "");
                    raster.ShortName = evValuesList[1].Replace("\"", "");
                    raster.Channel = (byte)StrToIntDef(evValuesList[2]);
                    if (evValuesList.Count > 3)
                        raster.TimeUnit = (byte)StrToIntDef(evValuesList[3]);
                    if (evValuesList.Count > 4)
                        raster.TimeCycle = StrToIntDef(evValuesList[4]);                    
                    events.Add(raster);
                }
                catch (Exception exc)
                {
                    Module.ErrorLog.AddError("Error Parsing RASTER " + exc.Message + $"  {string.Join("", str)}");
                }
            }
            return events;
        }

        private List<XcpDaq> GetCcpSource(string cleanedStr)
        {
            List<XcpDaq> ccpSources = new();
            foreach (string str in GetFirstInstanceTextBetween(cleanedStr, CCP_SOURCE_START, CCP_SOURCE_END))
            {
                try
                {
                    string pattern = @"/\*.*?\*/|//.*?$"; // Regex to match /* */ style comments and // comments
                    string cleanedSourceStr = Regex.Replace(str, pattern, "", RegexOptions.Singleline | RegexOptions.Multiline);
                    var sourceValuesList = Regex.Matches(cleanedSourceStr, @"([""'].*?[""']|\S+)").ToList();
                    XcpDaq source = new();
                    source.Name = sourceValuesList[0].Replace("\"", "");
                    if (cleanedSourceStr.Contains("QP_BLOB"))
                        for (int i = 0; i < sourceValuesList.Count; i++)
                        {
                            if (sourceValuesList[i].ToUpper() == "QP_BLOB" && sourceValuesList.Count > i + 1)
                                source.DaqIndex = (byte)StrToIntDef(sourceValuesList[i + 1]);
                            else if (sourceValuesList[i].ToUpper() == "LENGTH" && sourceValuesList.Count > i + 1)
                                source.MaxOdt = (byte)StrToIntDef(sourceValuesList[i + 1]);
                            else if (sourceValuesList[i].ToUpper() == "CAN_ID_FIXED" && sourceValuesList.Count > i + 1)
                                source.Ident = (uint)StrToIntDef(sourceValuesList[i + 1]);
                            else if (sourceValuesList[i].ToUpper() == "FIRST_PID" && sourceValuesList.Count > i + 1)
                                source.FirstPid = (short)StrToIntDef(sourceValuesList[i + 1]);
                            else if (sourceValuesList[i].ToUpper() == "RASTER" && sourceValuesList.Count > i + 1)
                            {
                                //source.EventFixed = true;
                                source.EventChannel = (byte)StrToIntDef(sourceValuesList[i + 1]);
                            }
                            source.Ident = source.Ident & 0x1FFFFFFF;
                        }
                    ccpSources.Add(source);
                }
                catch (Exception exc)
                {
                    Module.ErrorLog.AddError("Error Parsing SOURCE " + exc.Message + $"  {string.Join("", str)}");
                }
            }
            return ccpSources;
        }

       
    }

}
