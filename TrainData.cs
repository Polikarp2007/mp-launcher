using System.Collections.Generic;

namespace PoliCoLauncherApp
{
    public class TrainData
    {
        public string TrainType { get; set; } = "";
        public string TrainNumber { get; set; } = "";
        public string StartStation { get; set; } = "";
        public string EndStation { get; set; } = "";
        public string DepartureTime { get; set; } = "";
        public string Locomotive { get; set; } = "";
        public int WagonCount { get; set; }
        public List<string> WagonNumbers { get; set; } = new();
        public Dictionary<string, int> IntermediateStopMinutes { get; set; } = new();
    }
}
