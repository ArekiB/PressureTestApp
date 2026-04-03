using LiteDB;
using System;

namespace PressureTestApp.Models
{
    public class Measurement
    {
        [BsonId]
        public ObjectId Id { get; set; }

        public string SessionId { get; set; }
        public string TestName { get; set; }
        public DateTime Timestamp { get; set; }
        public double Pressure { get; set; }
        public int Index { get; set; }
    }
}