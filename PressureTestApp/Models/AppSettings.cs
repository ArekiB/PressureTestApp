namespace PressureTestApp.Models
{
    public class AppSettings
    {
        public string LastEmulationType { get; set; } = "Static";
        public double StaticValue { get; set; } = 150.0;
        public double RampStep { get; set; } = 1.0;
        public double RandomLimit { get; set; } = 200.0;
        public string LastTestName { get; set; } = "Испытание";
    }
}