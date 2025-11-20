using System;
using System.Collections.Generic;
using System.Text;

namespace RXD.Objects
{
    internal class TraceEvent : TraceRow, IRecordGridItem
    {
        public string strTimestamp => string.Format("{0:0.000000}", FloatTimestamp);

        public string strBusChannel => "";

        public string strCanID => EventTriggerName;

        public string strFlags => "";

        public string strDLC => "";

        public byte Value { get; set; }

        public string EventTriggerName { get; set; }

        public string strData => Value.ToString();
    }
}
