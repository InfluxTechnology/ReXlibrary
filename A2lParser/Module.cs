using A2lParserLib.CompuMethods;
using A2lParserLib.Items;
using A2lParserLib.Settings;
using System.Collections.Generic;
using System.Linq;

namespace A2lParserLib
{
    public class Module
    {
        public string Name { get; set; }
        public List<Measurement> Measurements { get; set; }
        public List<Characteristic> Characteristics { get; set; }
        public Dictionary<string, CompuMethod> CompuMethods { get; set; }
        public ErrorLog ErrorLog { get; set; } = new ErrorLog();
        public XcpSettings XcpSettings { get; set; }
        public List<XcpSettings> XcpSettingsAll { get; set; }

        public List<IA2lItem> AllItems { get => Measurements.Cast<IA2lItem>().Concat(Characteristics.Cast<IA2lItem>()).ToList(); }
        
    }
}
