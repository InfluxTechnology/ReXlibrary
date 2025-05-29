using A2lParserLib.CompuMethods;
using A2lParserLib.Items;
using A2lParserLib.Settings;
using System;
using System.Collections.Generic;
using System.Linq;
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

        public string FileName { get; set; }
        public string FileNameSerialized { get; set; }
        public Module Module { get; private set; }
        public async Task<Module> LoadA2l(string filePath, Action<object> ProgressCallback = null)
        {
            var data = System.IO.File.ReadAllText(filePath, Encoding.UTF8);
            Module = new Module();
            Module.XcpSettingsAll = await GetXcpSettings(data);
            Module.XcpSettings = Module.XcpSettingsAll[0];
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
                    if (Module.CompuMethods.TryGetValue(match.Groups[2].Value.Cleanup(), out CompuMethod compuMethod))
                    {
                        measVar.CompuMethod = compuMethod;
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
                size *= uint.Parse(dimension);
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
                    Match match = Regex.Match(cleanedInput.Substring(nameRegex.Length + 1), pattern);
                    if (match.Groups[1].Value.Cleanup().ToUpper() == "VALUE")
                    {
                        item.EcuAddress = StrToIntDef(match.Groups[2].Value.Cleanup());
                        item.DataType = match.Groups[3].Value.Cleanup().ToDataType();
                        item.Size = item.DataType.ToSize();
                        if (Module.CompuMethods.TryGetValue(match.Groups[6].Value.Cleanup(), out CompuMethod compuMethod))
                        {
                            item.CompuMethod = compuMethod;
                        }
                        item.MinValue = double.Parse(match.Groups[7].Value.Cleanup());
                        item.MaxValue = double.Parse(match.Groups[9].Value.Cleanup());
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
            string pattern = @"(\S+)+\s+""([^""]*)""+\s+(\S+)+\s+""([^""]*)""+\s+""([^""]*)""";
            foreach (string compuMethodStr in GetFirstInstanceTextBetween(data, CM_ST_START, CM_ST_END))
            {
                Match match = Regex.Match(compuMethodStr.Cleanup(), pattern);
                try
                {
                    if (!CompuTables.ContainsKey(match.Groups[1].Value.Cleanup()))
                    {
                        CompuMethod compMet = null;
                        CompuMethodType compuType = match.Groups[3].Value.Cleanup().ToEnum<CompuMethodType>();
                        if (compuType == CompuMethodType.RAT_FUNC)
                        {
                            compMet = new RatFunc();
                        }
                        else if (compuType == CompuMethodType.TAB_VERB)
                        {
                            compMet = new CompuTable();
                        }
                        else 
                        if (compMet is null)
                            continue;
                        compMet.Name = match.Groups[1].Value.Cleanup();
                        compMet.Description = match.Groups[2].Value.Cleanup();
                        compMet.Type = match.Groups[3].Value.Cleanup().ToEnum<CompuMethodType>();
                        compMet.FormatString = match.Groups[4].Value.Cleanup();
                        compMet.Units = match.Groups[5].Value.Cleanup();
                        //if (compMet.Type == CompuMethodType.TAB_VERB)
                        //    (compMet as CompuTable).RefTable = GetStringAfter(compuMethodStr, "COMPU_TAB_REF").Cleanup();
                        if (compMet.Type == CompuMethodType.RAT_FUNC)
                            GetCoeffs(compMet as RatFunc, GetStringAfter(compuMethodStr, "COEFFS").Cleanup());



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
                    compMet.Coeffs[2] = Convert.ToDouble(coeffs[2]) / Convert.ToDouble(coeffs[1]);
                    compMet.Coeffs[4] = Convert.ToDouble(coeffs[4]);
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

        private void GetTableValues(CompuMethod compMet, uint size, string compuMethodStr)
        {
            
        }

        async Task<Dictionary<string, CompuTable>> GetCompuMethodVTabs(string data)
        {
            Dictionary<string, CompuTable> CompuMethodVTabs = new();
            foreach (string compuMethodVTabStr in GetFirstInstanceTextBetween(data, CMV_ST_START, CMV_ST_END))
            {
                string pattern = @"(\S+)+\s+""([^""]*)""+\s+(\S+)+\s+(\d+)";
                Match match = Regex.Match(compuMethodVTabStr.Cleanup(), pattern);
                try
                {
                    if (!CompuMethodVTabs.ContainsKey(match.Groups[0].Value.Cleanup()))
                    {
                        CompuTable table = new CompuTable();
                        table.Name = match.Groups[1].Value.Cleanup();
                        table.Description = match.Groups[2].Value.Cleanup();
                        table.Type = match.Groups[3].Value.Cleanup().ToEnum<CompuMethodType>();
                        table.NumberOfPairs = int.Parse(match.Groups[4].Value.Cleanup());
                        table.Values = new Dictionary<int, object>();
                        string[] strSplt = compuMethodVTabStr.Trim().Substring(match.Length + 1).Cleanup().Split('\n');
                        for (int i = 0; i < table.NumberOfPairs; i++)
                        {
                            var cmptSplt = strSplt[i].Cleanup().Trim().Split(' ');
                            table.Values.Add(int.Parse(cmptSplt[0].Cleanup()), cmptSplt.Length == 1 ? "" : cmptSplt[1].Cleanup());
                        }
                        CompuMethodVTabs.Add(table.Name, table);
                    }
                }
                catch (Exception exc)
                {
                    Module.ErrorLog.AddError(exc.Message + $"  {string.Join("", compuMethodVTabStr)}");
                }
                
                    
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
                var protocolLayerList = SplitSettings(cleanedStr, @"/begin(\s*)PROTOCOL_LAYER");
                foreach (var protocolLayer in protocolLayerList)
                {
                    try
                    {
                        XcpSettings settings = new();
                        settingsList.Add(settings);
                        cleanedStr = protocolLayer.ToString();
                        
                        settings.Cmmds = Regex.Matches(cleanedStr, @"OPTIONAL_CMD\s+(\w+)").ToList();  //Get optional commands
                        if (cleanedStr.Contains("BYTE_ORDER_MSB_FIRST"))
                            settings.ByteOrder = ByteOrder.Motorola;
                        else
                            settings.ByteOrder = ByteOrder.Intel;
                        var res = Regex.Matches(cleanedStr, @"/begin\s+DAQ\s+(\S+)\s+(\S+)\s+(\S+)\s+(\S+)").ToList();  //Get Daq type (static, dynamic)
                        if (res.Count > 3)
                        {
                            settings.DaqType = res[0].ToUpper() == "STATIC" ? DaqType.Static : DaqType.Dynamic;
                            settings.MaxDaq = StrToIntDef(res[1].Cleanup());
                            settings.MaxEventChannels = StrToIntDef(res[2].Cleanup());
                            settings.MinDaq = (byte)StrToIntDef(res[3].Cleanup());
                        }
                        settings.Daqs = GetDaqLists(cleanedStr);
                        settings.Events = GetEvents(cleanedStr);
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
                                daq.Ident = settings.Dto;
                                daq.MaxOdt = maxOdt;
                                daq.MaxOdtEntries = 7;
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
                    string pattern = @"/\*.*?\*/|//.*?$"; // Regex to match /* */ style comments and // comments
                    string cleanedStr = Regex.Replace(str, pattern, "", RegexOptions.Singleline | RegexOptions.Multiline);
                    settings.StationAddress = (ushort)StrToIntDef(Regex.Match(cleanedStr, @"^\s*(\S+)").Groups[0].Value.Cleanup());
                    settings.Cro = (uint)StrToIntDef(Regex.Match(cleanedStr, @"CAN_ID_MASTER\s+(\S+)").Groups[1].Value.Cleanup());
                    settings.Dto = (uint)StrToIntDef(Regex.Match(cleanedStr, @"CAN_ID_SLAVE\s+(\S+)")?.Groups[1].Value.Cleanup());
                    settings.Baudrate = (uint)StrToIntDef(Regex.Match(cleanedStr, @"BAUDRATE\s+(\S+)")?.Groups[1].Value.Cleanup());


                    foreach (string match in GetFirstInstanceTextBetween(data, @"/begin(\s*)DAQ_LIST_CAN_ID", @"/begin(\s*)DAQ_LIST_CAN_ID"))
                    {

                    }
                    var daqList = Regex.Matches(cleanedStr, @"/begin DAQ_LIST_CAN_ID\s*([\s\S]*?)\s*/end DAQ_LIST_CAN_ID").ToList();
                    foreach (var daqStr in daqList)
                    {
                        uint daqIdx = StrToIntDef(Regex.Match(daqStr, @"(\S+)").Groups[0].Value.Cleanup());
                        var daq = settings.Daqs.FirstOrDefault(x => x.DaqIndex == daqIdx);
                        if (daq is not null && daqStr.Contains("FIXED"))
                        {
                            daq.Ident = StrToIntDef(Regex.Match(daqStr, @"FIXED\s+(\S+)").Groups[1].Value.Cleanup());
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
                    xcpEvent.Name = evValuesList[0];
                    xcpEvent.ShortName = evValuesList[1];
                    xcpEvent.Channel = (byte)StrToIntDef(evValuesList[2]);
                    if (evValuesList.Count > 4)
                        xcpEvent.MaxDaqList = (byte)StrToIntDef(evValuesList[4]);
                    if (evValuesList.Count > 5)
                        xcpEvent.TimeCycle = (byte)StrToIntDef(evValuesList[5]);
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

        

    }

}
