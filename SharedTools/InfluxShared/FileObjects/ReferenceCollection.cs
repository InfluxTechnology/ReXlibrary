using System;
using System.Collections.Generic;
using System.Linq;

namespace InfluxShared.FileObjects
{
    public class ReferenceChannel
    {
        public byte BusChannelIndex { get; set; }

        public string FileName { get; set; }
    }

    public class ReferenceDbcChannel : ReferenceChannel
    {
        public UInt32 MessageID { get; set; }

        public string SignalName { get; set; }

        public static bool operator ==(ReferenceDbcChannel item1, ReferenceDbcChannel item2) =>
            item1.BusChannelIndex == item2.BusChannelIndex &&
            item1.FileName == item2.FileName &&
            item1.MessageID == item2.MessageID &&
            item1.SignalName == item2.SignalName;
        public static bool operator !=(ReferenceDbcChannel item1, ReferenceDbcChannel item2) => !(item1 == item2);
    }

    public class ReferenceLdfChannel : ReferenceChannel
    {
        public byte MessageID { get; set; }

        public string SignalName { get; set; }

        public static bool operator ==(ReferenceLdfChannel item1, ReferenceLdfChannel item2) =>
            item1.BusChannelIndex == item2.BusChannelIndex &&
            item1.FileName == item2.FileName &&
            item1.MessageID == item2.MessageID &&
            item1.SignalName == item2.SignalName;
        public static bool operator !=(ReferenceLdfChannel item1, ReferenceLdfChannel item2) => !(item1 == item2);
    }

    public class ReferenceA2LChannel : ReferenceChannel
    {
        public UInt32 Address { get; set; }

        public string SignalName { get; set; }

        public static bool operator ==(ReferenceA2LChannel item1, ReferenceA2LChannel item2) =>
            item1.BusChannelIndex == item2.BusChannelIndex &&
            item1.FileName == item2.FileName &&
            item1.Address == item2.Address &&
            item1.SignalName == item2.SignalName;
        public static bool operator !=(ReferenceA2LChannel item1, ReferenceA2LChannel item2) => !(item1 == item2);
    }

    public class ExportCollections
    {
        public ExportDbcCollection dbcCollection;
        public ExportLdfCollection ldfCollection;
        //public ExportA2LCollection a2lCollection;

        public ExportCollections()
        {
            dbcCollection = new ExportDbcCollection();
            ldfCollection = new ExportLdfCollection();
            //a2lCollection = new ExportA2LCollection();
        }
    }

    public class ReferenceCollection : List<ReferenceChannel>
    {
        internal readonly ObjectLibrary ObjLibrary;

        public ReferenceCollection(ObjectLibrary AObjLibrary)
        {
            ObjLibrary = AObjLibrary;
        }

        public void Add(ReferenceChannel channel)
        {
            if (this.Where(c => c.GetType() == channel.GetType()).FirstOrDefault(c => c == channel) != null)
                return;

            base.Add(channel);
        }

        public ExportCollections GetExportCollections()
        {
            ExportCollections config = new ExportCollections();

            foreach (var channel in this)
                if (channel is ReferenceDbcChannel)
                {
                    var dbc = ObjLibrary.DBCFiles.FirstOrDefault(d => d.FileNameSerialized == channel.FileName);
                    if (dbc is null)
                        continue;
                    var msg = dbc.Messages.FirstOrDefault(m => m.FullID == (channel as ReferenceDbcChannel).MessageID);
                    if (msg is null)
                        continue;
                    var sig = msg.Items.FirstOrDefault(s => s.Name == (channel as ReferenceDbcChannel).SignalName);
                    if (sig is null)
                        break;

                    config.dbcCollection.AddMessage(channel.BusChannelIndex, msg).AddSignal(sig);
                }
                else if (channel is ReferenceLdfChannel)
                {
                    var ldf = ObjLibrary.LDFFiles.FirstOrDefault(d => d.FileNameSerialized == channel.FileName);
                    if (ldf is null)
                        continue;
                    var msg = ldf.Messages.FirstOrDefault(m => m.ID == (channel as ReferenceLdfChannel).MessageID);
                    if (msg is null)
                        continue;
                    var sig = msg.Items.FirstOrDefault(s => s.Name == (channel as ReferenceLdfChannel).SignalName);
                    if (sig is null)
                        break;

                    config.ldfCollection.AddMessage(channel.BusChannelIndex, msg).AddSignal(sig);
                }
                /*else if (channel is ReferenceA2LChannel)
                {
                    var a2l = ObjLibrary.A2LFiles.FirstOrDefault(d => d.FileNameSerialized == channel.FileName);
                    if (a2l is null)
                        continue;
                    var msg = a2l.Items.FirstOrDefault(m => m.Ident == (channel as ReferenceA2LChannel).Address);
                    if (msg is null)
                        break;

                    config.a2lCollection.AddItem(channel.BusChannelIndex, msg);
                }*/

            return config;
        }

        public List<DBC> GetAssignedDbc()
        {
            var dbclist = new List<DBC>();
            foreach (var channel in this.OfType<ReferenceDbcChannel>())
            {
                var dbc = ObjLibrary.DBCFiles.FirstOrDefault(d => d.FileNameSerialized == channel.FileName);
                if (dbc is null)
                    continue;
                if (!dbclist.Contains(dbc))
                    dbclist.Add(dbc);
            }
            return dbclist;
        }

        public List<LDF> GetAssignedLdf()
        {
            var ldflist = new List<LDF>();
            foreach (var channel in this.OfType<ReferenceLdfChannel>())
            {
                var ldf = ObjLibrary.LDFFiles.FirstOrDefault(d => d.FileNameSerialized == channel.FileName);
                if (ldf is null)
                    continue;
                if (!ldflist.Contains(ldf))
                    ldflist.Add(ldf);
            }
            return ldflist;
        }

       /* public List<A2lParser> GetAssignedA2L()
        {
            var a2llist = new List<A2lParser>();
            foreach (var channel in this.OfType<ReferenceA2LChannel>())
            {
                var a2l = ObjLibrary.A2LFiles.FirstOrDefault(d => d.FileNameSerialized == channel.FileName);
                if (a2l is null)
                    continue;
                if (!a2llist.Contains(a2l))
                    a2llist.Add(a2l);
            }
            return a2llist;
        }*/

        public bool IsInUse(object obj)
        {
            if (obj is DBC)
            {
                foreach (var item in this)
                    if (item.FileName == (obj as DBC).FileNameSerialized)
                        return true;
            }
            else if (obj is LDF)
            {
                foreach (var item in this)
                    if (item.FileName == (obj as LDF).FileNameSerialized)
                        return true;
            }
           /* else if (obj is A2lParser)
            {
                foreach (var item in this)
                    if (item.FileName == (obj as A2lParser).FileNameSerialized)
                        return true;
            }*/

            return false;
        }
    }
}
