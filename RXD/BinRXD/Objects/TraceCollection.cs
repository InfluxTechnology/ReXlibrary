using InfluxShared.FileObjects;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace RXD.Objects
{
    public class TraceCollection : List<TraceRow>
    {
        public static bool ShowFullStaticItems = true;
        public static int LimitStaticItemsCount = 10;
        public DateTime StartLogTime = DateTime.Now;
        internal List<IItemGridDetails> StaticValuesCollection = new();
        internal readonly object _staticValuesLock = new();

        public TraceCollection()
        {

        }

        internal void InitStaticValues()
        {
            lock (_staticValuesLock)
            {
                StaticValuesCollection = this.OfType<IItemGridDetails>().GroupBy(i => i.strItemName).Select(x => x.Last()).ToList();
                if (!ShowFullStaticItems)
                    StaticValuesCollection = StaticValuesCollection.Take(LimitStaticItemsCount).ToList();
            }
        }

        internal void UpdateStaticValues(List<IItemGridDetails> newData)
        {
            int FindItem(string key, out IItemGridDetails item)
            {
                for (int i = 0; i < StaticValuesCollection.Count; i++)
                {
                    item = StaticValuesCollection[i];
                    if (item.strItemName == key)
                        return i;
                }

                item = null;
                return -1;
            }

            lock (_staticValuesLock)
                foreach (var data in newData)
                {
                    var idx = FindItem(data.strItemName, out IItemGridDetails item);
                    if (item == null)
                    {
                        if (!ShowFullStaticItems)
                            if (StaticValuesCollection.Count >= LimitStaticItemsCount)
                                return;
                        StaticValuesCollection.Add(data);
                    }
                    else
                        StaticValuesCollection[idx] = data;
                }
        }

        public string asASCII
        {
            get
            {
                string ascii = "";
                foreach (var rec in this.OfType<ITraceConvertAdapter>())
                    ascii += rec.asASCII;
                return ascii;
            }
        }

        public bool ToASCII(string FileName, Action<object> ProgressCallback)
        {
            try
            {
                using (ASC asc = new ASC())
                {
                    if (asc.Start(FileName, StartLogTime))
                    {
                        ProgressCallback?.Invoke(0);
                        ProgressCallback?.Invoke("Writing ASCII file...");
                        for (int i = 0; i < Count; i++)
                        {
                            if (this[i] is ITraceConvertAdapter)
                                asc.WriteLine((this[i] as ITraceConvertAdapter).asASCII);
                            ProgressCallback?.Invoke(i * 100 / Count);
                        }
                        ProgressCallback?.Invoke(100);
                        return true;
                    }
                }
                return false;
            }
            catch
            {
                return false;
            }
        }

        public bool ToASCII(Stream ascStream, Action<object> ProgressCallback)
        {
            try
            {
                using (ASC asc = new ASC())
                {
                    if (asc.Start(ascStream, StartLogTime))
                    {
                        ProgressCallback?.Invoke(0);
                        ProgressCallback?.Invoke("Writing ASCII stream...");
                        for (int i = 0; i < Count; i++)
                        {
                            if (this[i] is ITraceConvertAdapter)
                                asc.WriteLine((this[i] as ITraceConvertAdapter).asASCII);
                            ProgressCallback?.Invoke(i * 100 / Count);
                        }
                        ProgressCallback?.Invoke(100);
                        return true;
                    }
                }
                return false;
            }
            catch
            {
                return false;
            }
        }

        public string asTRC
        {
            get
            {
                string trc = "";
                foreach (var rec in this.OfType<ITraceConvertAdapter>())
                    trc += rec.asTRC;
                return trc;
            }
        }

        public bool ToTRC(string FileName, Action<object> ProgressCallback)
        {
            try
            {
                using (TRC trc = new TRC())
                {
                    if (trc.Start(FileName, StartLogTime))
                    {
                        ProgressCallback?.Invoke(0);
                        ProgressCallback?.Invoke("Writing TRC file...");
                        for (int i = 0; i < Count; i++)
                        {
                            if (this[i] is ITraceConvertAdapter)
                                trc.WriteLine((this[i] as ITraceConvertAdapter).asTRC);
                            ProgressCallback?.Invoke(i * 100 / Count);
                        }
                        ProgressCallback?.Invoke(100);
                        return true;
                    }
                }
                return false;
            }
            catch
            {
                return false;
            }
        }

        public bool ToTRC(Stream traceStream, Action<object> ProgressCallback)
        {
            try
            {
                using (TRC trc = new TRC())
                {
                    if (trc.Start(traceStream, StartLogTime))
                    {
                        ProgressCallback?.Invoke(0);
                        ProgressCallback?.Invoke("Writing TRC stream...");
                        for (int i = 0; i < Count; i++)
                        {
                            if (this[i] is ITraceConvertAdapter)
                                trc.WriteLine((this[i] as ITraceConvertAdapter).asTRC);
                            ProgressCallback?.Invoke(i * 100 / Count);
                        }
                        ProgressCallback?.Invoke(100);
                        return true;
                    }
                }
                return false;
            }
            catch
            {
                return false;
            }
        }

        public string asCST
        {
            get
            {
                string cst = "";
                foreach (var rec in this.OfType<ITraceConvertAdapter>())
                    cst += rec.asCST;
                return cst;
            }
        }

        public bool ToCST(string FileName, Action<object> ProgressCallback)
        {
            try
            {
                using (CSTrace cst = new CSTrace())
                {
                    if (cst.Start(FileName, StartLogTime))
                    {
                        ProgressCallback?.Invoke(0);
                        ProgressCallback?.Invoke("Writing Comma separated trace file...");
                        for (int i = 0; i < Count; i++)
                        {
                            if (this[i] is ITraceConvertAdapter)
                                cst.WriteLine((this[i] as ITraceConvertAdapter).asCST);
                            ProgressCallback?.Invoke(i * 100 / Count);
                        }
                        ProgressCallback?.Invoke(100);
                        return true;
                    }
                }
                return false;
            }
            catch
            {
                return false;
            }
        }

        public bool ToCST(Stream traceStream, Action<object> ProgressCallback)
        {
            try
            {
                using (CSTrace cst = new CSTrace())
                {
                    if (cst.Start(traceStream, StartLogTime))
                    {
                        ProgressCallback?.Invoke(0);
                        ProgressCallback?.Invoke("Writing Comma separated trace stream...");
                        for (int i = 0; i < Count; i++)
                        {
                            if (this[i] is ITraceConvertAdapter)
                                cst.WriteLine((this[i] as ITraceConvertAdapter).asCST);
                            ProgressCallback?.Invoke(i * 100 / Count);
                        }
                        ProgressCallback?.Invoke(100);
                        return true;
                    }
                }
                return false;
            }
            catch
            {
                return false;
            }
        }

    }
}
