using System;
using System.IO;
using FietsApp.Recording;
using NUnit.Framework;
using UnityEngine;

namespace FietsApp.Tests
{
    /// <summary>De rit tijdens het rijden opslaan, zodat hij tot daar op Strava komt (story 5.2).</summary>
    public class RideSaveNowTests
    {
        RideRecorder _recorder;
        string _saved;

        [SetUp]
        public void SetUp()
        {
            _recorder = new GameObject("Opname").AddComponent<RideRecorder>();
            _recorder.Saved += path => _saved = path;
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(_recorder.gameObject);
            if (_saved != null && File.Exists(_saved)) File.Delete(_saved);
        }

        void Ride(int seconds)
        {
            var start = new DateTime(2001, 1, 1, 10, 0, 0, DateTimeKind.Utc); // oud genoeg om niets van de rijder te raken
            for (int i = 0; i < seconds; i++)
                _recorder.Log.Add(new RideSample(start.AddSeconds(i), 200f, 85f, 140f, 5f, i * 5f, 500f + i * 0.4f, double.NaN, double.NaN));
        }

        [Test]
        public void SaveNow_WritesRideSoFarAndKeepsRecordingAsNewRide()
        {
            Ride(40);
            Assert.IsTrue(_recorder.SaveNow());
            Assert.IsNotNull(_saved, "Saved meldt het bestand (voor de Strava-upload)");
            Assert.IsTrue(File.Exists(_saved));
            Assert.AreEqual(0, _recorder.Log.Count, "daarna begint een nieuwe opname");
        }

        [Test]
        public void SaveNow_TooShort_SavesNothing()
        {
            Ride(10);
            Assert.IsFalse(_recorder.SaveNow());
            Assert.IsNull(_saved);
            Assert.AreEqual(10, _recorder.Log.Count, "de korte rit loopt gewoon door");
        }
    }
}
