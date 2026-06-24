using InfluxDB.Client.Api.Domain;
using InfluxShared.FileObjects;
using RXD.Base;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Cloud.Export
{
    internal static class CsvMultipartHelper
    {
        internal static async Task<bool> ToCsv(IStorageProvider storage, string bucket, string destFile, BinRXD rxd, ExportCollections signalsCollection, ILogProvider? log = null)
        {
            log?.Log($"Start CSV convert");
            ProcessingRulesCollection rules = null;
            try
            {
                if (Config.ConfigJson.CSV.enabled == true && Config.ConfigJson.CSV.resampling.enabled == true)
                {
                    rules = new ProcessingRulesCollection()
                    {
                        GeneralRules = new ProcessingRules(rules)
                        {
                            InitialTimestamp = SyncTimestampLogic.FirstSample,
                            SampleAfterEnd = true,
                            SampleBeforeBeginning = true,
                            SamplingMethod = SamplingValueSource.LastValue,
                            SamplingRate = Config.ConfigJson.CSV.resampling.rate
                        }
                    };
                }
            }
            catch (Exception exc)
            {
                log?.Log($"Processing Rule Exception {exc.Message}");
            }
            var exportCsv = new BinRXD.ExportSettings()
            {
                StorageCache = StorageCacheType.Memory,
                SignalsDatabase = signalsCollection,
                ProcessingRules = rules,
            };
            var ddcCsv = rxd.ToDoubleData(exportCsv);
            if (ddcCsv is null)
            {
                log?.Log($"Double Data is null. No data for CSV export");
                return false;
            }
            log?.Log($"Double data points count {ddcCsv.Count}");
            if (storage.GetType().ToString().Contains("AwsS3StorageProvider"))
                return await ddcCsv.ToCSVMultipart(storage, bucket, destFile, log);
            else
                return await ddcCsv.ToCSV(storage, bucket, destFile, log);
        }

        internal static async Task<bool> ToCSV(this DoubleDataCollection ddc, IStorageProvider storage, string bucket, string key, ILogProvider? log = null)
        {
            try
            {
                ddc.InitReading();
                var ci = new CultureInfo("en-US", false);
                MemoryStream csvStream = new MemoryStream();
                DateTime creationTime = ddc.RealTime;
                using (StreamWriter stream = new StreamWriter(csvStream))
                {                    

                    stream.Write(
                        "Creation Time : " + creationTime.ToString("dd/MM/yy HH:mm") + Environment.NewLine +
                        "Time," + string.Join(",", ddc.Select(n => n.ChannelName)) + Environment.NewLine +
                        new string(',', ddc.Count) + Environment.NewLine +
                        new string(',', ddc.Count) + Environment.NewLine +
                        "sec," + string.Join(",", ddc.Select(n => n.ChannelUnits)) + Environment.NewLine
                    );

                    double[] Values = ddc.GetValues();
                    string line = "";
                    while (Values != null)
                    {
                        //double temp = ddc.RealTime.ToOADate() + Values[0] / 86400;
                        //log?.Log($"double={temp}     datetime={DateTime.FromOADate(temp).ToString("dd/MM/yyyy HH:mm:ss.fff")}");
                        stream.WriteLine(DateTime.FromOADate(creationTime.ToOADate() + Values[0] / 86400).ToString("dd/MM/yyyy HH:mm:ss.fff") + "," +
                            string.Join(",", Values.Select(x => x.ToString(ci)).ToArray(), 1, Values.Length - 1).Replace("NaN", ""));

                        Values = ddc.GetValues();
                    }
                    log?.Log($"Uploading CSV");
                    stream.Flush();
                    if (csvStream.Length > 0)
                    {
                        csvStream.Seek(0, SeekOrigin.Begin);
                        log?.Log($"Memory after CSV created: {GC.GetTotalMemory(false) / (1024 * 1024)} MB");
                        await storage.UploadFile(bucket, key, csvStream);
                    }
                    return true;

                }

            }
            catch (Exception e)
            {
                log?.Log($"Create CSV failed: {e.Message}");
                return false;
            }
        }

        internal static async Task<bool> ToCSVMultipart(this DoubleDataCollection ddc, IStorageProvider storage, string bucket, string key, ILogProvider? log = null)
        {

            string uploadId = await storage.InitiateMultipartUpload(bucket, key);

            try
            {
                log?.Log($"Initiated multipart upload {uploadId}");

                var ci = new CultureInfo("en-US", false);

                ddc.InitReading();
                DateTime creationTime = ddc.RealTime;
                log?.Log($"ddc count {ddc.Count}");
                int partId = 1;
                MemoryStream csvStream = new MemoryStream();
                using (StreamWriter stream = new StreamWriter(csvStream, new UTF8Encoding(false), 1024, true))
                {
                    async Task S3Upload()
                    {
                        csvStream.Seek(0, SeekOrigin.Begin);
                        csvStream.Position = 0;
                        log?.Log($"Uploading part {partId} with size {csvStream.Length}");
                        await storage.UploadPart(csvStream, bucket, key, uploadId, partId);
                        //log?.Log($"Uploaded part {partId}");
                        partId++;
                        csvStream.Seek(0, SeekOrigin.Begin);
                        csvStream.Position = 0;
                        csvStream.SetLength(0);
                    }
                    stream.Write(
                        "Creation Time : " + creationTime.ToString("dd/MM/yy HH:mm") + Environment.NewLine +
                        "Time," + string.Join(",", ddc.Select(n => n.ChannelName)) + Environment.NewLine +
                        new string(',', ddc.Count) + Environment.NewLine +
                        new string(',', ddc.Count) + Environment.NewLine +
                        "sec," + string.Join(",", ddc.Select(n => n.ChannelUnits)) + Environment.NewLine
                    );

                    double[] Values = ddc.GetValues();
                    while (Values != null)
                    {
                        //double temp = ddc.RealTime.ToOADate() + Values[0] / 86400;
                        //log?.Log($"double={temp}     datetime={DateTime.FromOADate(temp).ToString("dd/MM/yyyy HH:mm:ss.fff")}");
                        stream.WriteLine(
                            DateTime.FromOADate(creationTime.ToOADate() + Values[0] / 86400).ToString("dd/MM/yyyy HH:mm:ss.fff") + "," +
                            string.Join(",", Values.Select(x => x.ToString(ci)).ToArray(), 1, Values.Length - 1).Replace("NaN", ""));

                        if (csvStream.Length >= 5 * 1024 * 1024)
                        {
                            stream.Flush();
                            await S3Upload();
                        }

                        Values = ddc.GetValues();
                    }
                    log?.Log($"Final upload initiation. Length is:{csvStream.Length}");
                    stream.Flush();
                    if (csvStream.Length > 0)
                        await S3Upload();

                    await storage.CompleteMultipartUpload(bucket, key, uploadId);
                }

                return true;
            }
            catch (Exception e)
            {
                log?.Log($"An AmazonS3Exception was thrown: {e.Message}");

                // Abort the upload.
                await storage.AbortMultipartUpload(bucket, key, uploadId);

                return false;
            }
        }
    }
}
