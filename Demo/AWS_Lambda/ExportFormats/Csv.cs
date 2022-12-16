﻿using RXD.Base;
using Influx.Shared.Helpers;
using InfluxShared.FileObjects;
using Amazon.S3;
using InfluxDB.Client.Api.Domain;
using InfluxDB.Client;
using System.Globalization;
using System.Net.Security;
using System.Text;
using Amazon.Lambda.Core;
using Amazon.S3.Model;
using System.Net.Sockets;
using Amazon.Runtime;

namespace AWSLambdaFileConvert.ExportFormats
{
    internal static class CsvMultipartHelper
    {
        public static ILambdaContext? Context { get; set; } //Used to write information to log filesS

        internal static async Task<bool> ToCSVMultipart(this DoubleDataCollection ddc, string bucket, string key)
        {
            try
            {
                var s3Client = new AmazonS3Client();

                List<UploadPartResponse> uploadResponses = new List<UploadPartResponse>();
                InitiateMultipartUploadResponse initResponse =
                    await s3Client.InitiateMultipartUploadAsync(new()
                    {
                        BucketName = bucket,
                        Key = key
                    });
                long partSize = 5 * 1024 * 1024;
                Context?.Logger.LogInformation($"Initiated multipart upload {initResponse.UploadId}");

                var ci = new CultureInfo("en-US", false);

                ddc.InitReading();
                int partId = 1;
                MemoryStream csvStream = new MemoryStream();
                using (StreamWriter stream = new StreamWriter(csvStream, Encoding.UTF8, 1024, true))
                {
                    async Task S3Upload()
                    {
                        UploadPartRequest uploadRequest = new UploadPartRequest
                        {
                            BucketName = bucket,
                            Key = key,
                            UploadId = initResponse.UploadId,
                            PartNumber = partId,
                            InputStream = csvStream
                        };

                        // Track upload progress.
                        //uploadRequest.StreamTransferProgress +=
                            //new EventHandler<StreamTransferProgressArgs>(UploadPartProgressEventCallback);

                        // Upload a part and add the response to our list.
                        uploadResponses.Add(await s3Client.UploadPartAsync(uploadRequest));
                        Context?.Logger.LogInformation($"Uploaded part {partId}");
                        partId++;
                        csvStream.Seek(0, SeekOrigin.Begin);
                        csvStream.Position = 0;
                        csvStream.SetLength(0);
                    }

                    stream.Write(
                        "Creation Time : " + ddc.RealTime.ToString("dd/MM/yy HH:mm") + Environment.NewLine +
                        "Time," + string.Join(",", ddc.Select(n => n.ChannelName)) + Environment.NewLine +
                        new string(',', ddc.Count) + Environment.NewLine +
                        new string(',', ddc.Count) + Environment.NewLine +
                        "sec," + string.Join(",", ddc.Select(n => n.ChannelUnits)) + Environment.NewLine
                    );

                    double[] Values = ddc.GetValues();
                    while (Values != null)
                    {
                        stream.WriteLine(
                            DateTime.FromOADate(ddc.RealTime.ToOADate() + Values[0] / 86400).ToString("dd/MM/yyyy HH:mm:ss.fff") +
                            string.Join(",", Values.Select(x => x.ToString(ci)).ToArray(), 1, Values.Length - 1).Replace("NaN", ""));

                        if (csvStream.Length > 5 * 1024 * 1024)
                            await S3Upload();

                        Values = ddc.GetValues();
                    }
                    if (csvStream.Length > 5 * 1024 * 1024)
                        await S3Upload();

                    CompleteMultipartUploadRequest completeRequest = new CompleteMultipartUploadRequest
                    {
                        BucketName = bucket,
                        Key = key,
                        UploadId = initResponse.UploadId
                    };
                    completeRequest.AddPartETags(uploadResponses);

                    // Complete the upload.
                    CompleteMultipartUploadResponse completeUploadResponse =
                        await s3Client.CompleteMultipartUploadAsync(completeRequest);
                }

                return true;
            }
            catch (Exception e)
            {
                Context?.Logger.LogInformation(e.Message);
                return false;
            }
        }
    }
}
