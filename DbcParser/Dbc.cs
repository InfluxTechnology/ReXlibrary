using DbcParserLib.Model;
using System.Collections.Generic;

namespace DbcParserLib
{
    public class Dbc
    {
        public enum MsgType { Standard, Extended, CanFDStandard, CanFDExtended, J1939PG, Lin }
        public IEnumerable<Node> Nodes { get; }
        public IEnumerable<Message> Messages { get; }

        public Dbc(IEnumerable<Node> nodes, IEnumerable<Message> messages)
        {
            Nodes = nodes;
            Messages = messages;
        }
        //       public MsgType GetMsgType(uint ID)
        //       {
        //return MsgType.Standard;
        //}
    }
}