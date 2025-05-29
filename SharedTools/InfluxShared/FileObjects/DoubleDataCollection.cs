using InfluxShared.Helpers;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace InfluxShared.FileObjects
{
    public enum TimeFormatType { Seconds, DateTime }

    public class DoubleDataCollection : List<DoubleData>, IDisposable
    {
        public const string Extension = ".csv";
        public const string Filter = "Comma separated values (*.csv)|*.csv";
        public const string svExtension = ".csv";
        public const string svFilter = "Separated values (*.csv)|*.csv";

        readonly string TempLocation;
        readonly StorageCacheType StorageCache;
        internal TimeFormatType DefaultCsvDateFormat = TimeFormatType.Seconds;
        private bool disposedValue = false;
        internal bool ObjectOwner = true;
        SortedSet<double> ChannelTimes = new SortedSet<double>();

        internal readonly string DisplayName;
        internal DateTime RealTime = DateTime.Now;
        internal ProcessingRulesCollection ProcessingRules = null;

        public double ReadingProgress => this.Average(d => d.ReadProgress);

        public DoubleDataCollection(string DisplayName, StorageCacheType StorageCache, string TempLocation = null)
        {
            this.DisplayName = DisplayName;
            this.StorageCache = StorageCache;
            this.TempLocation = (TempLocation is null) ? PathHelper.TempPath : TempLocation;
        }

        #region Destructors
        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing && ObjectOwner)
                {
                    foreach (DoubleData data in this)
                        data.Dispose();
                }

                // TODO: free unmanaged resources (unmanaged objects) and override finalizer
                // TODO: set large fields to null
                disposedValue = true;
            }
        }

        // // TODO: override finalizer only if 'Dispose(bool disposing)' has code to free unmanaged resources
        // ~DoubleDataCollection()
        // {
        //     // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        //     Dispose(disposing: false);
        // }

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
        #endregion

        /// <summary>
        /// Add double data channel
        /// </summary>
        /// <param name="identifier"></param>
        /// Unique identifier for each channel.
        /// Message frame uses binary block unique id
        /// DataFrame identifier structure is:
        /// bit 0..15: message index from dbcCollection or ldfCollection
        /// bit 16..29: signal index from message list
        /// bit 30..32: Type (1 - CAN DataFrame, 2- LIN DataFrame)
        /// bit 33..40: Source Address
        /// <param name="ChannelName"></param>
        /// Channel name
        /// <param name="ChannelUnits"></param>
        /// Channel units
        /// <returns></returns>
        public DoubleData Add(UInt64 identifier, string ChannelName = "", string ChannelUnits = "")
        {
            DoubleData data = new DoubleData(StorageCache, TempLocation)
            {
                identifier = identifier,
                ChannelName = ChannelName,
                ChannelUnits = ChannelUnits
            };

            Add(data);
            if (ProcessingRules is not null)
            {
                ProcessingRules.Add(data);
                data.InjectTransformer(ProcessingRules[data]);
            }
            return data;
        }

        public bool ObjectExist(UInt64 identifier)
        {
            foreach (DoubleData data in this)
                if (data.identifier == identifier)
                    return true;

            return false;
        }

        public DoubleData GetObject(UInt64 identifier)
        {
            foreach (DoubleData data in this)
                if (data.identifier == identifier)
                    return data;

            return null;
        }

        public DoubleData AddOrGet(UInt64 identifier, string ChannelName = "", string ChannelUnits = "")
        {
            foreach (DoubleData data in this)
                if (data.identifier == identifier)
                    return data;

            return Add(identifier,
                ChannelName: ChannelName,
                ChannelUnits: ChannelUnits
            );
        }

        public DoubleDataCollection Filtered(List<UInt16> FilterIdList)
        {
            DoubleDataCollection data = new(DisplayName, StorageCache, TempLocation)
            {
                ObjectOwner = false,
                ProcessingRules = ProcessingRules
            };
            foreach (DoubleData dd in this)
                if (FilterIdList.Contains((UInt16)dd.identifier))
                    data.Add(dd);

            return data;
        }

        public void SortByIdentifier()
        {
            Sort((x, y) => x.identifier.CompareTo(y.identifier));
        }

        internal void InitReading()
        {
            ChannelTimes.Clear();
            foreach (DoubleData data in this)
            {
                data.InitReading();
                if (!double.IsNaN(data.TempTime))
                    ChannelTimes.Add(data.TempTime);
            }

            //FirstTimestamp = double.IsNaN(FirstTimestamp) ? FirstTimestamp = LowestTime() : Math.Min(FirstTimestamp, LowestTime());
        }

        internal void FinishWrite(double EndTime)
        {
            if (ProcessingRules is null)
                return;

            foreach (DoubleData data in this)
                data.FinishWrite(EndTime);
        }

        public double[] GetValues(double Timestamp = double.NaN)
        {
            if (double.IsNaN(Timestamp))
                Timestamp = LowestTime();

            bool finished = true;
            double[] ValList = new double[Count + 1];
            ValList[0] = Timestamp/* - FirstTimestamp*/;
            for (int i = 0; i < Count; i++)
            {
                if (this[i].TempEof)
                {
                    ValList[i + 1] = double.NaN;
                }
                else
                {
                    finished = false;
                    if (this[i].TempTime == Timestamp)
                    {
                        ValList[i + 1] = this[i].TempData;
                        this[i].ReadNext();
                        if (!double.IsNaN(this[i].TempTime))
                            ChannelTimes.Add(this[i].TempTime);
                    }
                    else
                        ValList[i + 1] = double.NaN;
                }
            }

            if (finished)
                return null;
            else
                return ValList;
        }

        double LowestTime()
        {
            // new logic for fast detection of lowest timestamp for huge amount of signals
            if (ChannelTimes.Count == 0)
                return double.NaN;

            double TempTime = ChannelTimes.First();
            ChannelTimes.Remove(TempTime);
            return TempTime;

            /* Old logic 
            double TempTime = double.NaN;
            foreach (DoubleData data in this)
                if (!double.IsNaN(data.TempTime))
                {
                    if (double.IsNaN(TempTime))
                        TempTime = data.TempTime;
                    else
                        TempTime = Math.Min(TempTime, data.TempTime);
                }
            return TempTime;//*/
        }

        public bool ToSV(string csvFileName, Action<object> ProgressCallback = null) => ToSV(csvFileName, DefaultCsvDateFormat, ProgressCallback);

        public bool ToSV(string csvFileName, TimeFormatType TimeFormat, Action<object> ProgressCallback = null)
        {
            using (FileStream fs = new FileStream(csvFileName, FileMode.Create))
                return ToSV(fs, TimeFormat, ProgressCallback);
        }

        public bool ToSV(Stream csvStream, Action<object> ProgressCallback = null) => ToSV(csvStream, DefaultCsvDateFormat, ProgressCallback);

        public bool ToSV(Stream svStream, TimeFormatType TimeFormat, Action<object> ProgressCallback = null)
        {
            var ci = CultureInfo.CurrentCulture;
            string sep = ci.TextInfo.ListSeparator;

            Func<double, string> TimestampToString = TimeFormat switch
            {
                TimeFormatType.Seconds => delegate (double ts) { return ts.ToString("0.00000", ci); }
                ,
                TimeFormatType.DateTime => delegate (double ts) { return DateTime.FromOADate(RealTime.ToOADate() + ts / 86400).ToString("yyyy-MM-dd HH:mm:ss.fff"); }
                ,
                _ => delegate (double ts) { return ""; }
                ,
            };

            try
            {
                ProgressCallback?.Invoke(0);
                ProgressCallback?.Invoke("Writing SV file...");
                InitReading();
                using (StreamWriter stream = new StreamWriter(svStream, new UTF8Encoding(false), 1024, true))
                {
                    stream.Write(
                        "Creation Time : " + RealTime.ToString("yyyy-MM-dd HH:mm:ss.fff", ci) + Environment.NewLine +
                        "Time" + sep + string.Join(sep, this.Select(n => n.ChannelName)) + Environment.NewLine +
                        "sec" + sep + string.Join(sep, this.Select(n => n.ChannelUnits)) + Environment.NewLine
                    );

                    double[] Values = GetValues();
                    while (Values != null)
                    {
                        stream.WriteLine(
                            TimestampToString(Values[0]) + sep +
                            string.Join(sep, Values.Select(x => x.ToString(ci)).ToArray(), 1, Values.Length - 1).Replace("NaN", ""));

                        Values = GetValues();
                        ProgressCallback?.Invoke((int)(ReadingProgress * 100));
                    }
                }

                ProgressCallback?.Invoke(100);
                return true;
            }
            catch (Exception e)
            {
                //MessageBox.Show(e.Message);
                return false;
            }
        }

        public bool ToCSV(string csvFileName, Action<object> ProgressCallback = null) => ToCSV(csvFileName, DefaultCsvDateFormat, ProgressCallback);

        public bool ToCSV(string csvFileName, TimeFormatType TimeFormat, Action<object> ProgressCallback = null)
        {
            using (FileStream fs = new FileStream(csvFileName, FileMode.Create))
                return ToCSV(fs, TimeFormat, ProgressCallback);
        }

        public bool ToCSV(Stream csvStream, Action<object> ProgressCallback = null) => ToCSV(csvStream, DefaultCsvDateFormat, ProgressCallback);

        public bool ToCSV(Stream csvStream, TimeFormatType TimeFormat, Action<object> ProgressCallback = null)
        {
            Func<double, string> TimestampToString = TimeFormat switch
            {
                TimeFormatType.Seconds => delegate (double ts) { return ts.ToString("0.00000"); }
                ,
                TimeFormatType.DateTime => delegate (double ts) { return 
                    DateTime.FromOADate(RealTime.ToOADate() + ts / 86400).ToString("dd/MM/yyyy HH:mm:ss.") // .fff
                    + ts.ToString("0.00000").Split('.')[1];
                }
                ,
                _ => delegate (double ts) { return ""; }
                ,
            };

            try
            {
                var ci = new CultureInfo("en-US", false);

                ProgressCallback?.Invoke(0);
                ProgressCallback?.Invoke("Writing CSV file...");
                InitReading();
                using (StreamWriter stream = new StreamWriter(csvStream, new UTF8Encoding(false), 1024, true))
                {
                    stream.Write(
                        "Creation Time : " + RealTime.ToString("dd/MM/yy HH:mm") + Environment.NewLine +
                        "Time," + string.Join(",", this.Select(n => n.ChannelName)) + Environment.NewLine +
                        new string(',', Count) + Environment.NewLine +
                        new string(',', Count) + Environment.NewLine +
                        "sec," + string.Join(",", this.Select(n => n.ChannelUnits)) + Environment.NewLine
                    );

                    double[] Values = GetValues();
                    while (Values != null)
                    {
                        stream.WriteLine(
                            TimestampToString(Values[0]) + ", " +
                            string.Join(",", Values.Select(x => x.ToString("0.################", ci)).ToArray(), 1, Values.Length - 1).Replace("NaN", ""));

                        Values = GetValues();
                        ProgressCallback?.Invoke((int)(ReadingProgress * 100));
                    }
                }

                ProgressCallback?.Invoke(100);
                return true;
            }
            catch (Exception e)
            {
                //MessageBox.Show(e.Message);
                return false;
            }
        }

        public bool ToInfluxDBCSV(string csvFileName, Action<object> ProgressCallback = null)
        {
            using (FileStream fs = new FileStream(csvFileName, FileMode.Create))
                return ToInfluxDBCSV(fs, ProgressCallback);
        }

        public bool ToInfluxDBCSV(Stream csvStream, Action<object> ProgressCallback = null)
        {
            try
            {
                var ci = new CultureInfo("en-US", false);

                ProgressCallback?.Invoke(0);
                ProgressCallback?.Invoke("Writing CSV file...");
                InitReading();
                using (StreamWriter stream = new StreamWriter(csvStream, new UTF8Encoding(false), 1024, true))
                {
                    stream.Write(
                        "#datatype measurement,tag,double,dateTime:RFC3339" + Environment.NewLine +
                        "device,signal,measurement,time" + Environment.NewLine
                    );

                    double[] Values = GetValues();
                    while (Values != null)
                    {
                        for (int i = 1; i < Values.Length; i++)
                            if (!double.IsNaN(Values[i]))
                            {
                                stream.WriteLine(
                                    DisplayName + "," +
                                    this[i - 1].ChannelName + ',' +
                                    Values[i].ToString(ci) + ',' +
                                    DateTime.FromOADate(RealTime.ToOADate() + Values[0] / 86400).ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss.fffZ")
                                );
                            }
                        Values = GetValues();
                        ProgressCallback?.Invoke((int)(ReadingProgress * 100));
                    }
                }

                ProgressCallback?.Invoke(100);
                return true;
            }
            catch (Exception e)
            {
                //MessageBox.Show(e.Message);
                return false;
            }
        }

       
    }
}
