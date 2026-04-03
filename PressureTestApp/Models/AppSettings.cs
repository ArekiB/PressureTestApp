namespace PressureTestApp.Models
{
    public class AppSettings
    {
        public string LastEmulationType { get; set; } = "Static";
        public double StaticValue { get; set; } = 150.0;
        public double RampStep { get; set; } = 1.0;
        public double RandomLimit { get; set; } = 200.0;
        public string LastTestName { get; set; } = "Испытание";

        public bool UseModbus { get; set; } = false;
        public string ModbusIpAddress { get; set; } = "127.0.0.1";
        public int ModbusPort { get; set; } = 502;
        public int ModbusSlaveId { get; set; } = 1;
        public int ModbusRegisterAddress { get; set; } = 0;
        public string ModbusDataType { get; set; } = "float";
        public double ModbusScale { get; set; } = 1.0;

        public List<ModbusRegister> ModbusRegisters { get; set; } = new List<ModbusRegister>();
    }
}