using Amazon.Lambda.Core;
using Amazon.TimestreamWrite;
using Amazon.TimestreamWrite.Model;
using InfluxShared.FileObjects;

namespace AWSLambdaFileConvert.ExportFormats
{
    internal static class TimeStreamHelper
    {
        internal class atsSettings
        {
            public bool enabled { get; set; }
            public string db_name { get; set; }
            public string table_name { get; set; }
        }

        public static ILambdaContext? Context { get; set; } //Used to write information to log filesS

        public static async Task<bool> ToAwsTimeStream(this DoubleDataCollection ddc, atsSettings settings)
        {
            var writeClient = new AmazonTimestreamWriteClient();
            var writeRecordsRequest = new WriteRecordsRequest
            {
                DatabaseName = settings.db_name,
                TableName = settings.table_name,
                Records = new()
            };

            try
            {

                List<Dimension> dimensions = new List<Dimension>
                {
                    new Dimension { Name = "device_id", Value = ddc.DisplayName }
                };

                Context?.Logger.LogInformation("Created Dimension");
                ddc.InitReading();

                double[] Values = ddc.GetValues();
                while (Values != null)
                {
                    for (int i = 1; i < Values.Length; i++)
                        if (!double.IsNaN(Values[i]))
                        {
                            writeRecordsRequest.Records.Add(new Record
                            {
                                Dimensions = dimensions,
                                MeasureName = ddc[i - 1].ChannelName,
                                MeasureValue = Values[i].ToString(),
                                MeasureValueType = MeasureValueType.DOUBLE,
                                Time = Math.Truncate(((ddc.RealTime.ToOADate() - 25569) * 86400 + Values[0]) * 1000).ToString()
                            });
                            //Context?.Logger.LogInformation(records[records.Count-1].Time.ToString());
                            if (writeRecordsRequest.Records.Count >= 50)
                            {
                                Context?.Logger.LogInformation($"Writing {writeRecordsRequest.Records.Count} records");
                                await writeClient.LocalWriteRecordsAsync(writeRecordsRequest);
                                Context?.Logger.LogInformation($"Records {writeRecordsRequest.Records.Count} written");
                                writeRecordsRequest.Records.Clear();
                            }
                        }
                    Values = ddc.GetValues();
                }
                if (writeRecordsRequest.Records.Count > 0)
                {
                    Context?.Logger.LogInformation($"Writing {writeRecordsRequest.Records.Count} records");
                    await writeClient.LocalWriteRecordsAsync(writeRecordsRequest);
                    Context?.Logger.LogInformation($"Records {writeRecordsRequest.Records.Count} written");
                    writeRecordsRequest.Records.Clear();
                }

                return true;
            }
            catch (Exception e)
            {
                Context?.Logger.LogInformation(e.Message);
                return false;
            }
        }

        private static async Task<bool> LocalWriteRecordsAsync(this AmazonTimestreamWriteClient writeClient, WriteRecordsRequest request)
        {
            Context?.Logger.LogInformation("Writing records");

            try
            {
                WriteRecordsResponse response = await writeClient.WriteRecordsAsync(request);
                Context?.Logger.LogInformation($"Write records status code: {response.HttpStatusCode.ToString()}");
            }
            catch (RejectedRecordsException e)
            {
                Context?.Logger.LogInformation("RejectedRecordsException:" + e.ToString());
                foreach (RejectedRecord rr in e.RejectedRecords)
                    Context?.Logger.LogInformation("RecordIndex " + rr.RecordIndex + " : " + rr.Reason);

                Context?.Logger.LogInformation("Other records were written successfully. ");
            }
            catch (Exception e)
            {
                Context?.Logger.LogInformation("Write records failure:" + e.ToString());
            }

            return true;
        }
    }


}
