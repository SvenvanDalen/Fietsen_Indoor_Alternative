using NUnit.Framework;
using UnityEngine;

namespace FietsApp.Tests
{
    public class RideHoldTests
    {
        RideController _ride;

        [SetUp]
        public void SetUp() => _ride = new GameObject("Rit").AddComponent<RideController>();

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_ride.gameObject);

        [Test]
        public void ClosingTheMenuDoesNotEndThePause()
        {
            object pause = "pauze", menu = "menu";
            _ride.Hold(pause);
            _ride.Hold(menu);
            _ride.Release(menu);
            Assert.That(_ride.enabled, Is.False, "nog gepauzeerd");
            Assert.That(_ride.IsHeldBy(pause), Is.True);
            _ride.Release(pause);
            Assert.That(_ride.enabled, Is.True);
        }

        [Test]
        public void ReleasingWithoutHoldIsHarmless()
        {
            _ride.Release("niemand");
            Assert.That(_ride.enabled, Is.True);
        }
    }
}
