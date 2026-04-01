using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using LiteDB;
using PressureTestApp.Models;

namespace PressureTestApp.Services
{
    public class LiteDbService : IDataService, IDisposable
    {
        private readonly LiteDatabase _database;
        private readonly ILiteCollection<TestSession> _sessions;
        private readonly ILiteCollection<Measurement> _measurements;

        public LiteDbService(string databasePath = null)
        {
            if (string.IsNullOrEmpty(databasePath))
            {
                string dataDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data");
                if (!Directory.Exists(dataDir))
                {
                    Directory.CreateDirectory(dataDir);
                }
                databasePath = Path.Combine(dataDir, "testdata.db");
            }

            _database = new LiteDatabase(databasePath);
            _sessions = _database.GetCollection<TestSession>("sessions");
            _measurements = _database.GetCollection<Measurement>("measurements");

            _measurements.EnsureIndex(x => x.TestName);
            _measurements.EnsureIndex(x => x.Timestamp);
            _sessions.EnsureIndex(x => x.Name);
            _sessions.EnsureIndex(x => x.StartTime);
        }

        public void SaveMeasurement(Measurement measurement)
        {
            _measurements.Insert(measurement);
        }

        public List<Measurement> GetMeasurementsByTestName(string testName)
        {
            return _measurements.Find(x => x.TestName == testName).ToList();
        }

        public List<TestSession> GetAllSessions()
        {
            return _sessions.FindAll().OrderByDescending(x => x.StartTime).ToList();
        }

        public TestSession GetSession(string sessionId)
        {
            return _sessions.FindById(sessionId);
        }

        public void SaveSession(TestSession session)
        {
            _sessions.Upsert(session);
        }

        public void DeleteSession(string sessionId)
        {
            var session = GetSession(sessionId);
            if (session != null)
            {
                _measurements.DeleteMany(x => x.TestName == session.Name);
                _sessions.Delete(sessionId);
            }
        }

        public void Dispose()
        {
            _database?.Dispose();
        }
    }
}