using System;

namespace OpcDAToMSA.UI.Forms
{
    public class PointRow
    {
        public string Tag { get; set; }
        public string Code { get; set; }
        public object Value { get; set; }
        public string Quality { get; set; }
        public DateTime Timestamp { get; set; }
        public DateTime LastChanged { get; set; }
    }
}


