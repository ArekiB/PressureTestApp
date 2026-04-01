using System.Collections.Generic;
using PressureTestApp.Models;

namespace PressureTestApp.Services
{
    public interface IDataService
    {
        void SaveMeasurement(Measurement measurement);
        List<Measurement> GetMeasurementsByTestName(string testName);
        List<TestSession> GetAllSessions();
        TestSession GetSession(string sessionId);
        void SaveSession(TestSession session);
        void DeleteSession(string sessionId);
    }
}