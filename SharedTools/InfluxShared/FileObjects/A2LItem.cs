namespace InfluxShared.FileObjects
{
    public class A2LItem : BasicItemInfo
    {
        public A2LItem()
        {

        }

        //public A2LItemType MsgType { get; set; }
       // public A2LValueТype Datatype { get; set; }
      //  public A2LByteOrder ByteOrder { get; set; }
        public byte ShLeft { get; set; }
        public byte ShRight { get; set; }
        public uint BitMask { get; set; }
        public bool Selected { get; set; }
    }
}
