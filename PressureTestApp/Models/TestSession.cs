using System;

namespace PressureTestApp.Models
{
    public class TestSession
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string DisplayName { get; set; }    
        public string Name { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public string EmulationType { get; set; }
        public double Param1 { get; set; }
        public int PointsCount { get; set; }
    }
}