using System;

namespace RXD.Blocks
{
    public class BinConfigS3 : BinBase
    {
        public enum S3_Connection_Type : byte
        {
            PLAIN,
            SSL
        }
        public enum S3_TYPE : byte
        {
            AWS,
            COMPATIBLE,
            GCS
        }

        internal enum BinProp
        {
            S3Type,
            EndPointSize,
            EndPoint,
            BucketSize,
            Bucket,
            RegionSize,
            Region,
            ConnectionType,
            ConnectionPort,
            AccessKeySize,
            AccessKey,
            SecretKeySize,
            SecretKey,
            ConfigurationCheckTime,
            FirmwareCheckTime,
            StatusSendTime,
            AutomaticFirmwareUpdate,
            EncryptPass,
            KeepLogFilesOnDevice,
            RootCertificateSize,
            RootCertificate,
            UploadAllLogsBeforeSleep,
            UploadLogs,
            UploadStatus
        }

        #region Do not touch these
        public BinConfigS3(BinHeader hs = null) : base(hs) { }

        internal dynamic this[BinProp index]
        {
            get => data.GetProperty(index.ToString());
            set => data.SetProperty(index.ToString(), value);
        }
        #endregion

        internal override void SetupVersions()
        {
            Versions[1] = new Action(() =>
            {
                data.AddProperty(BinProp.S3Type, typeof(S3_TYPE));
                data.AddProperty(BinProp.EndPointSize, typeof(byte));
                data.AddProperty(BinProp.EndPoint, typeof(string), BinProp.EndPointSize);
                data.AddProperty(BinProp.BucketSize, typeof(byte));
                data.AddProperty(BinProp.Bucket, typeof(string), BinProp.BucketSize);
                data.AddProperty(BinProp.RegionSize, typeof(byte));
                data.AddProperty(BinProp.Region, typeof(string), BinProp.RegionSize);
                data.AddProperty(BinProp.ConnectionType, typeof(S3_Connection_Type));
                data.AddProperty(BinProp.ConnectionPort, typeof(UInt16));
                data.AddProperty(BinProp.AccessKeySize, typeof(byte));
                data.AddProperty(BinProp.AccessKey, typeof(string), BinProp.AccessKeySize);
                data.AddProperty(BinProp.SecretKeySize, typeof(byte));
                data.AddProperty(BinProp.SecretKey, typeof(string), BinProp.SecretKeySize);
                data.AddProperty(BinProp.ConfigurationCheckTime, typeof(UInt32));
                data.AddProperty(BinProp.FirmwareCheckTime, typeof(UInt32));
                data.AddProperty(BinProp.StatusSendTime, typeof(UInt32));
                data.AddProperty(BinProp.AutomaticFirmwareUpdate, typeof(bool));
                data.AddProperty(BinProp.EncryptPass, typeof(bool));
                data.AddProperty(BinProp.KeepLogFilesOnDevice, typeof(bool));
                data.AddProperty(BinProp.RootCertificateSize, typeof(UInt16));
                data.AddProperty(BinProp.RootCertificate, typeof(string), BinProp.RootCertificateSize);
            });
            Versions[2] = new Action(() =>
            {
                Versions[1].DynamicInvoke();
                data.AddProperty(BinProp.UploadAllLogsBeforeSleep, typeof(bool), false);
            });
            Versions[3] = new Action(() =>
            {
                Versions[2].DynamicInvoke();
                data.AddProperty(BinProp.UploadLogs, typeof(bool), true);
                data.AddProperty(BinProp.UploadStatus, typeof(bool), true);
            });
        }
    }
}
