namespace PressureTestApp.Models
{
    public class ModbusRegister
    {
        public string Name { get; set; }        
        public int Address { get; set; }        // Адрес регистра
        public string Type { get; set; }        // "bool", "int", "float", "string"
        public double Scale { get; set; } = 1.0; // Множитель
        public bool Enabled { get; set; } = true;
    }
}