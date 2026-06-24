using InfluxDB.Client.Api.Domain;
using InfluxShared.FileObjects;
using InfluxShared.Interfaces;
using Minio.DataModel;
using RXD.Base;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net.Security;
using System.Text;

namespace Cloud.Export
{
    internal static class Parquet
    {
        internal static async Task<bool> ToParquet(IStorageProvider storage, string bucket, string destFile, BinRXD rxd, ExportCollections signalsCollection, ILogProvider? log = null)
        {
            int partId = 1;
            string uploadId = "";
            bool useFullDateTime = false;
            if (Config.ConfigJson.Parquet.enabled == true && Config.ConfigJson.Parquet.ContainsKey("use_datetime"))
            {
                useFullDateTime = Config.ConfigJson.Parquet.use_datetime;                
            }


                void WriteChunk(ParquetStream ms)
            {
                long offset = ms.offset + ms.baseLength;
                ms.baseSeek(0, SeekOrigin.Begin);
                ms.basePosition = 0;
                using (MemoryStream ps = new())
                {
                    ms.CopyTo(ps);
                    ps.Flush();
                    ps.Position = 0;
                    Task.Run(() => storage.UploadPart(ps, bucket, destFile, uploadId, partId)).Wait();
                    log?.Log($"Uploaded part {partId}     Size: {ps.Length}");
                }
                partId++;
                ms.baseSeek(0, SeekOrigin.Begin);
                ms.basePosition = 0;
                ms.baseSetLength(0);
                ms.offset = offset;
            }

            ProcessingRulesCollection rules = null;
            try
            {
                if (Config.ConfigJson.Parquet.enabled == true && Config.ConfigJson.Parquet.resampling.enabled == true)
                {
                    rules = new ProcessingRulesCollection()
                    {
                        GeneralRules = new ProcessingRules(rules)
                        {
                            InitialTimestamp = SyncTimestampLogic.FirstSample,
                            SampleAfterEnd = true,
                            SampleBeforeBeginning = true,
                            SamplingMethod = SamplingValueSource.LastValue,
                            SamplingRate = Config.ConfigJson.Parquet.resampling.rate
                        }
                    };
                }
            }
            catch (Exception exc)
            {
                log?.Log($"Processing Rule Exception {exc.Message}");
            }
            var exportParquet = new BinRXD.ExportSettings()
            {
                StorageCache = StorageCacheType.Memory,
                SignalsDatabase = signalsCollection,
                ProcessingRules = rules
            };
            var ddcParquet = rxd.ToDoubleData(exportParquet);
            ddcParquet.DefaultCsvDateFormat = useFullDateTime ? TimeFormatType.DateTime : TimeFormatType.Seconds;

            if (ddcParquet is null)
            {
                log?.Log($"Double Data is null. No data for Parquet export");
                return true;
            }
            else
            
            log?.Log($"Storage type is: {storage.GetType()}");

            if (storage.GetType().ToString().Contains("AwsS3StorageProvider"))
            {
                uploadId = await storage.InitiateMultipartUpload(bucket, destFile);
                MemoryStream ParqStream = new MemoryStream();
                if (ddcParquet.ToApacheParquet(WriteChunk))
                {
                    log?.Log($"Send final multipart upload");
                    await storage.CompleteMultipartUpload(bucket, destFile, uploadId);
                    return true;
                }
            }
            else
            {
                MemoryStream ParqStream = new MemoryStream();
                if (ddcParquet.ToApacheParquet(ParqStream))
                {
                    if (await storage.UploadFile(bucket, destFile, ParqStream))
                        log?.Log($"Apache Parquet written successfuly");
                    else
                        log?.Log($"Apache Parquet write to S3 failed");
                }
                else
                    log?.Log($"Failed to convert to Apache Parquet");
                return true;
            }
            return true;
        }

        

    }
}
