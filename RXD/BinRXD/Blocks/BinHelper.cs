using System.Collections.Generic;
using System.Linq;

namespace RXD.Blocks
{
    public static class BinHelper
    {
        public static Dictionary<ConditionType, string> ConditionNames = new Dictionary<ConditionType, string>()
        {
            { ConditionType.EQUAL, "=" },
            { ConditionType.GREATER, ">" },
            { ConditionType.LESS, "<" },
            { ConditionType.EQUAL_GREATER, ">=" },
            { ConditionType.EQUAL_LESS, "<=" },
            { ConditionType.NOT_EQUAL, "<>" },
            { ConditionType.NEW, "New" },
            { ConditionType.INCREMENT, "Increases" },
            { ConditionType.DECREMENT, "Decreases" },
            { ConditionType.CHANGE, "Changes" },
            { ConditionType.SAME, "Changes" },
            { ConditionType.IN_RANGE, "In range" },
            { ConditionType.NOT_IN_RANGE, "Not in range" },
            { ConditionType.IN_RANGE_INCLUSIVE, "In range (inclusive)" },
            { ConditionType.NOT_IN_RANGE_INCLUSIVE, "Not in range (inclusive)" },
        };

        public static string ToConditionString(this ConditionType condition) => ConditionNames.FirstOrDefault(x => x.Key == condition).Value;
        public static ConditionType ConditionTypeByName(string CondName) => ConditionNames.FirstOrDefault(x => x.Value == CondName).Key;

        #region Digitals
        public static Dictionary<ConditionType, string> DigitalConditionNames = new Dictionary<ConditionType, string>()
        {
            { ConditionType.INCREMENT, "Rises" },
            { ConditionType.DECREMENT, "Falls" },
            { ConditionType.CHANGE, "Changes" },
        };

        public static string ToDigitalConditionString(this ConditionType condition) => DigitalConditionNames.FirstOrDefault(x => x.Key == condition).Value;
        public static ConditionType DigitalConditionTypeByName(string CondName) => DigitalConditionNames.FirstOrDefault(x => x.Value == CondName).Key;
        #endregion
    }
}
