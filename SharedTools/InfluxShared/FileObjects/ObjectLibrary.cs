//using A2lParserLib;
using System.Collections.Generic;

namespace InfluxShared.FileObjects
{
    public delegate void ChangeNotifier();
    public delegate void BeforeRemoveNotifier(object node, ref bool AbortOperation);

    public class ObjectLibrary
    {
        public ChangeNotifier OnChange;

        public List<DBC> DBCFiles { get; set; }

       // public List<A2lParser> A2LFiles { get; set; }

        public List<LDF> LDFFiles { get; set; }

        public ObjectLibrary()
        {
            DBCFiles = new List<DBC>();
          //  A2LFiles = new List<A2lParser>();
            LDFFiles = new List<LDF>();
        }

        public void Clear()
        {
            DBCFiles.Clear();
          //  A2LFiles.Clear();
            LDFFiles.Clear();
            OnChange?.Invoke();
        }

    }
}
