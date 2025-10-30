using System;

namespace OpcDAToMSA.UI.Forms
{
    public class MetricRow
    {
        public string DisplayName { get; set; }
        public string Name { get; set; }
        public double Value { get; set; }
        public string Unit { get; set; }
        public DateTime Timestamp { get; set; }
    }
}


