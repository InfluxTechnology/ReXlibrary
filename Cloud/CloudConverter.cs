using Cloud.Export;
using DbcParserLib;
using DbcParserLib.Influx;
using Influx.Shared.Helpers;
using InfluxShared.FileObjects;
using MDF4xx.IO;
using RXD.Base;

namespace Cloud
{
    internal class CloudConverter
    {
        ILogProvider Log;
        IStorageProvider Storage;
        ITimeStreamProvider TimeStream;
        string Bucket;
        string LoggerDir;
        public FileLoaderFunc LoadFileMethod;

        public CloudConverter(ILogProvider logProvider, IStorageProvider storageProvider, ITimeStreamProvider timestream, string bucket, string loggerDir)
        {
            Log = logProvider;
            Storage = storageProvider;
            TimeStream = timestream;
            Bucket = bucket;
            LoggerDir = loggerDir;
        }
        public async Task<bool> Convert(string loggerDir, string filename, ConversionType conversion)
        {
            LoggerDir = loggerDir;
            if (!conversion.HasFlag(ConversionType.Csv) && !conversion.HasFlag(ConversionType.InfluxDB) &&
                   !conversion.HasFlag(ConversionType.TimeStream) && !conversion.HasFlag(ConversionType.Mdf)
                   && !conversion.HasFlag(ConversionType.Blf) && !conversion.HasFlag(ConversionType.Rxc)
                    && !conversion.HasFlag(ConversionType.Snapshot) && !conversion.HasFlag(ConversionType.Parquet))
            {
                Log?.Log("No valid Conversion requested!");
                return false;
            }

            if (conversion.HasFlag(ConversionType.Rxc))
            {
                return await XmlToRxcAsync(Bucket, filename);
            }
            else if (Path.GetExtension(filename).ToLower() == ".json")
            {
                if (filename.ToLower().Contains("snapshot"))
                {
                    if (conversion.HasFlag(ConversionType.Snapshot) && TimeStream != null)
                    {
                        var jsonStream = await GetFile(Bucket, filename.Replace(Bucket + '/', ""));
                        if (jsonStream != null)
                        {
                            using (StreamReader reader = new(jsonStream))
                            {
                                string json = reader.ReadToEnd();
                                int startIndex = loggerDir.IndexOf("_SN") + 3;
                                string sn = loggerDir.Substring(startIndex, 7);
                                await TimeStream.WriteSnapshot(sn, json, filename.Replace(Bucket + '/', ""));
                            }
                        }
                    }
                }
            }
            else if (Path.GetExtension(filename).ToLower() == ".rxd")
                try
                {
                    List<DBC?> dbcList = await LoadDBCList(Bucket);
                    Log?.Log("GetRxd!");
                    Stream rxdStream = await Storage.GetFile(Bucket, filename.Replace(Bucket + '/', ""));
                    Log?.Log($"Memory used: {GC.GetTotalMemory(false) / (1024 * 1024)} MB");
                    ExportDbcCollection signalsCollection = DbcToInfluxObj.LoadExportSignalsFromDBC(dbcList);
                    Log?.Log($"Memory after DBC used: {GC.GetTotalMemory(false) / (1024 * 1024)} MB");
                    if (Config.SynchConfig.enabled)
                    {
                        await SynchExportToMdf(filename);
                    }
                    else
                        using (BinRXD rxd = BinRXD.Load("http://" + filename, rxdStream))
                        {
                            Log?.Log($"Memory used after load RXD: {GC.GetTotalMemory(false) / (1024 * 1024)} MB");
                            if (rxd is null)
                            {
                                Log?.Log("Error loading RXD file");
                                return false;
                            }
                            else
                            {
                                var export = new BinRXD.ExportSettings()
                                {
                                    StorageCache = StorageCacheType.Memory,
                                    SignalsDatabase = new() { dbcCollection = signalsCollection }
                                };
                                Log?.Log($"Memory used after export settings created: {GC.GetTotalMemory(false) / (1024 * 1024)} MB");
                                /*foreach (var collection in export.SignalsDatabase.dbcCollection)
                                {
                                    Log?.Log($"ExportSettingsBUS:{collection.BusChannel} signals:{collection.Signals.Count}");
                                    foreach (var item in collection.Signals)
                                    {
                                        Log?.Log($"ExportSettingsBUS:{collection.BusChannel} signal:{item.Name}");
                                    }
                                }*/
                                BinRXD.MaxTimeGap = 0;
                                if (Config.ConfigJson.ContainsKey("RxdSettings") && Config.ConfigJson.RxdSettings.ContainsKey("MaxTimeGap"))
                                {
                                    if (Config.ConfigJson.RxdSettings.MaxTimeGap > 0)
                                    {
                                        BinRXD.MaxTimeGap = Config.ConfigJson.RxdSettings.MaxTimeGap * 1000 * 100;
                                        Log?.Log($"Using MaxTimeGap correction: {Config.ConfigJson.RxdSettings.MaxTimeGap} seconds");
                                    }                                    
                                }
                                DoubleDataCollection ddc = rxd.ToDoubleData(export);

                                Log?.Log($"Memory used after ddc: {GC.GetTotalMemory(false) / (1024 * 1024)} MB");

                                //Write to InfluxDB
                                if (conversion.HasFlag(Cloud.ConversionType.InfluxDB))
                                {
                                    Log?.Log("InfluxDB");

                                    await ddc.ToInfluxDB(Log);
                                }

                                //Write to timestream table
                                if (conversion.HasFlag(Cloud.ConversionType.TimeStream))
                                {
                                    int idx = filename.LastIndexOf('/');
                                    //Context?.Logger.LogInformation($"Correction in seconds is: {timeCorrection}");
                                    if (TimeStream != null)
                                    {
                                        var res = await TimeStream.ToTimeStream(ddc, filename.Substring(idx + 1, filename.Length - idx - 5));
                                        Log?.Log($"Writing to Timestream {res}");
                                    }

                                }

                                //Mdf Export
                                if (conversion.HasFlag(Cloud.ConversionType.Mdf))
                                {
                                    Log?.Log($"Starting Mdf conversion {rxd.Count}");
                                    MDF.UseCompression = true;      
                                    if (Config.ConfigJson.ContainsKey("MDF") && Config.ConfigJson.MDF.ContainsKey("usecompression"))
                                        MDF.UseCompression = Config.ConfigJson.MDF.usecompression;
                                    MemoryStream mdfStream = (MemoryStream)rxd.ToMF4(new BinRXD.ExportSettings() { SignalsDatabase = export.SignalsDatabase});
                                    if (mdfStream is null)
                                        Log?.Log($"Mdf Conversion failed");
                                    else
                                    {
                                        Log?.Log($"Mdf Stream Size: {mdfStream?.Length}");
                                        if (await Storage.UploadFile(Bucket, Path.ChangeExtension(filename, ".mf4"), mdfStream))
                                            Log?.Log($"Mdf written successfuly");
                                        else
                                            Log?.Log($"Mdf write to S3 failed");
                                    }

                                }
                                //BLF Export
                                if (conversion.HasFlag(Cloud.ConversionType.Blf))
                                {
                                    Log?.Log($"Starting Blf conversion {rxd.Count}");
                                    MemoryStream blfStream = new();
                                    rxd.ToBLF(blfStream, null);
                                    if (blfStream is null)
                                        Log?.Log($"Blf Conversion failed");
                                    else
                                    {
                                        Log?.Log($"Blf Stream Size: {blfStream?.Length}");
                                        if (await Storage.UploadFile(Bucket, Path.ChangeExtension(filename, ".blf"), blfStream))
                                            Log?.Log($"Blf written successfuly");
                                        else
                                            Log?.Log($"Blf write to S3 failed");
                                    }
                                }

                                //Parquet Export
                                if (conversion.HasFlag(Cloud.ConversionType.Parquet))
                                {
                                    Log?.Log($"Starting Parquet conversion {rxd.Count}");
                                    //if (Config.ConfigJson.ContainsKey("Parquet") && Config.ConfigJson.Parquet.ContainsKey("usecompression"))
                                    //    MDF.UseCompression = Config.ConfigJson.usecompression;
                                    await Export.Parquet.ToParquet(Storage, Bucket, Path.ChangeExtension(filename, ".parquet"), rxd, signalsCollection, Log);

                                }

                                //CSV Export
                                if (conversion.HasFlag(Cloud.ConversionType.Csv))
                                {
                                    Log?.Log($"Memory used before CSV: {GC.GetTotalMemory(false) / (1024 * 1024)} MB");
                                    await CsvMultipartHelper.ToCsv(Storage, Bucket, Path.ChangeExtension(filename, ".csv"), rxd, signalsCollection, Log);
                                }
                            }
                        }
                }
                catch (Exception e)
                {
                    Log?.Log("Error processing RXD file: " + e.Message);
                    return false;
                }
            return true;
        }

        private async Task SynchExportToMdf(string filename)
        {
            Log?.Log($"main_logger {Config.SynchConfig.main_logger}");
            if (Config.SynchConfig.main_logger != "" && Config.SynchConfig.addon_loger1 != "")
            {
                List<string> main_files;
                List<string> addon_files = new();

                if (filename != "")
                {
                    main_files = new List<string> { filename };
                    addon_files = await Storage.GetRxdFiles(Bucket, $"{Config.SynchConfig.addon_loger1}/{main_files[0].Split('/')[1]}");                    
                }
                else
                {
                    main_files = await Storage.GetRxdFiles(Bucket, Config.SynchConfig.main_logger);
                    List<uint> foldersInt = main_files.Select(file => uint.Parse(file.Split('/')[1])).Distinct().ToList();
                    foldersInt.Sort();
                    foreach (var item in foldersInt)
                    {
                        Log?.Log($"sorted {item}");
                    }
                    int idx = 0;
                    foreach (var item in foldersInt)
                    {
                        if (item.ToString() == Config.SynchConfig.lastfolder)
                        {
                            idx = foldersInt.IndexOf(item) + 1;
                            break;
                        }
                    }
                    if (idx < main_files.Count)
                    {
                        main_files = await Storage.GetRxdFiles(Bucket, $"{Config.SynchConfig.main_logger}/{foldersInt[idx]}");
                        addon_files = await Storage.GetRxdFiles(Bucket, $"{Config.SynchConfig.addon_loger1}/{foldersInt[idx]}");
                    }
                }
                
                foreach (var item in main_files)
                {
                    Log?.Log($"main_files {item}");
                }
                foreach (var item in addon_files)
                {
                    Log?.Log($"addon {item}");
                }
                if (main_files.Count > 0 && addon_files.Count > 0)
                {
                    LoadFileMethod = GetNextAddonFile;
                    
                    foreach (var masterFile in main_files)
                    {
                        Log?.Log($"Loading RXD master");
                        var masterStream = await Storage.GetFile(Bucket, masterFile);
                        BinRXD master = BinRXD.Load("http://" + masterFile, masterStream);
                        if (master is not null)
                        {
                            Log?.Log($"Master rxd loaded");
                            RXDLoggerCollection attached = new RXDLoggerCollection()
                                            {
                                                new RXDLogger("0002471", addon_files, LoadFileMethod),
                                            };
                            master.AttachedLoggers = attached;
                            MDF.UseCompression = true;
                            if (Config.ConfigJson.ContainsKey("MDF") && Config.ConfigJson.MDF.ContainsKey("usecompression"))
                                MDF.UseCompression = Config.ConfigJson.usecompression;
                            MemoryStream mdfStream = (MemoryStream)master.ToMF4();
                            if (mdfStream is null)
                                Log?.Log($"Mdf Conversion failed");
                            else
                            {
                                Log?.Log($"Mdf Stream Size: {mdfStream?.Length}");
                                if (await Storage.UploadFile(Bucket, Path.ChangeExtension(masterFile, ".mf4"), mdfStream))
                                    Log?.Log($"Mdf written successfuly");
                                else
                                    Log?.Log($"Mdf write to S3 failed");
                            }
                        }
                    }
                }
            }
        }

        private BinRXD GetNextAddonFile(string fileName)
        {
            Log?.Log("Loading Addon file: " + fileName);

            var addonStream = Task.Run(()=> Storage.GetFile("rexgensync", fileName)).Result;
            BinRXD master = BinRXD.Load("http://" + fileName, addonStream);
            Log?.Log($"Addon file: { fileName} loaded. Memory used: { GC.GetTotalMemory(false) / (1024 * 1024)} MB");
            return master;
        }

        private async Task<List<DBC?>> LoadDBCList(string bucket)
        {
            Log?.Log("Loading DBC");
            List<DBC?> listDbc = new();
            for (int i = 0; i < 4; i++)
            {
                string dbcPath = Path.Combine(LoggerDir, $"dbc_can{i}.dbc").Replace("\\", "/");
                Stream s3Stream = await Storage.GetFile(bucket, dbcPath);
                if (s3Stream is null)
                {
                    Log?.Log($"DBC File Not Found! {dbcPath}");
                    listDbc.Add(null);
                    continue;
                }
                Stream dbcStream = new MemoryStream();
                s3Stream.CopyTo(dbcStream);
                s3Stream.Dispose();
                dbcStream.Position = 0;
                Log?.Log($"DBC Stream size is {dbcStream.Length}");
                
                Parser dbcParser = new();
                Dbc dbc = dbcParser.ParseFromStream(dbcStream);
                Log?.Log($"DBC Messages count dbc_can{i}.dbc :" + dbc.Messages.ToList().Count.ToString());

                //Debug DBC messages
                /*string allmsg = "";
                foreach (var msg in dbc.Messages)
                {
                    allmsg += $"0x{msg.ID.ToString("X4")}:{msg.Name}" + Environment.NewLine;
                }
                Log?.Log(allmsg);*/

                if (dbc is null)
                {
                    Log?.Log("Error parsing DBC file");
                    listDbc.Add(null);
                    continue;
                }
                DBC influxDBC = (DbcToInfluxObj.FromDBC(dbc) as DBC);
                listDbc.Add(influxDBC);
            }
            return listDbc;
        }

        public async Task<Stream> GetFile(string bucket, string file)
        {
            return await Storage.GetFile(bucket, file);
        }

        public async Task<bool> XmlToRxcAsync(string bucket, string filename)
        {
            //The xsd schema must be in the same folder as the xml file            
            Stream? xsd = await Storage.GetFile(bucket, LoggerDir + "/ReXConfig.xsd");
            Stream? xml = await Storage.GetFile(bucket, filename);
            if (xsd != null && xml != null)
            {
                XmlConverter xmlConverter = new();
                Stream? rxc = xmlConverter.ConvertXMLToRxc(xsd, xml, Log);
                if (rxc != null)
                {
                    if (await Storage.UploadFile(Bucket, Path.ChangeExtension(filename, ".rxc"), rxc))
                        Log?.Log("RXC File Uploaded Successfully");                    
                    xsd?.Dispose();
                    xml?.Dispose();
                    rxc?.Dispose();
                    return true;
                }
            }            
            return false;
        }
    }

}
