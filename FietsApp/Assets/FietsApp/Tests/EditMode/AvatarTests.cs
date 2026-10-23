using FietsApp.Training;
using NUnit.Framework;
using UnityEngine;

namespace FietsApp.Tests
{
    /// <summary>Avatar en fietskeuze (story 7.3).</summary>
    public class AvatarTests
    {
        [Test]
        public void Bike_WeightCountsInThePhysics()
        {
            var p = new RiderProfile { weightKg = 70f };
            Assert.AreEqual(78f, p.TotalMassKg, 1e-4f, "standaard de racefiets van 8 kg, zoals voorheen");
            p.avatar.bike = (int)BikeKind.Climbing;
            Assert.AreEqual(76.8f, p.TotalMassKg, 1e-4f);
            p.avatar.bike = (int)BikeKind.Classic;
            Assert.AreEqual(80.5f, p.TotalMassKg, 1e-4f);
            p.avatar.bike = 99;
            Assert.AreEqual(BikeKind.Allround, p.Bike.Kind, "onbekende fiets valt terug op de racefiets");
            Assert.Less(Bike.Of(BikeKind.Aero).DragAreaDeltaM2, 0f, "aerofiets heeft minder luchtweerstand");
        }

        [Test]
        public void Avatar_OldProfileGetsDefaults()
        {
            var old = JsonUtility.FromJson<RiderProfile>("{\"ftpWatts\": 250, \"weightKg\": 70}");
            Assert.IsNotNull(old.avatar);
            Assert.AreEqual(BikeKind.Allround, old.Bike.Kind);
            Assert.AreEqual(78f, old.TotalMassKg, 1e-4f);
        }

        [Test]
        public void Avatar_ColorsWrapAround()
        {
            var a = new AvatarSettings { jersey = -1, helmet = AvatarSettings.Colors.Length, skin = 7 };
            Assert.AreEqual(AvatarSettings.Colors[AvatarSettings.Colors.Length - 1], a.JerseyColor);
            Assert.AreEqual(AvatarSettings.Colors[0], a.HelmetColor);
            Assert.AreEqual(AvatarSettings.SkinTones[2], a.SkinColor);
            Assert.AreEqual(AvatarSettings.Colors.Length, AvatarSettings.ColorNames.Length);
        }

        [Test]
        public void Avatar_SyncsWithTheNewestProfile()
        {
            var laptop = new RiderProfile { updatedAt = "2026-10-20T10:00:00Z" };
            var pc = new RiderProfile { updatedAt = "2026-10-22T10:00:00Z" };
            pc.avatar.bike = (int)BikeKind.Gravel;
            pc.avatar.jersey = 3;
            var merged = ProfileMerge.Merge(laptop, pc);
            Assert.AreEqual(BikeKind.Gravel, merged.Bike.Kind);
            Assert.AreEqual(3, merged.avatar.jersey);
            Assert.AreNotSame(pc.avatar, merged.avatar, "kopie, niet hetzelfde object");
        }

        /// <summary>
        /// Zithouding: onderin de slag buigt de knie 25-35° (gangbare fietsafstelling), bovenin 100-120°, en het been
        /// staat nooit gestrekt. Referentie los nagerekend: 25,4° en 105,5° voor de racefiets.
        /// </summary>
        [Test]
        public void Legs_BendLikeARealRider()
        {
            foreach (BikeKind kind in System.Enum.GetValues(typeof(BikeKind)))
            {
                var g = new CyclistGeometry(kind);
                float straightest = 180f, most = 0f;
                for (int degree = 0; degree < 360; degree += 2)
                {
                    float angle = degree * Mathf.Deg2Rad;
                    var (ankle, _) = g.Ankle(angle, right: true);
                    var knee = CyclistGeometry.Joint(g.HipRight, ankle, g.Thigh, g.Shin, Vector3.forward);
                    Assert.AreEqual(g.Thigh, Vector3.Distance(g.HipRight, knee), 1e-3f);
                    Assert.AreEqual(g.Shin, Vector3.Distance(knee, ankle), 1e-3f);
                    Assert.Greater(knee.z, Mathf.Min(g.HipRight.z, ankle.z), "knie naar voren");
                    float bend = 180f - Vector3.Angle(g.HipRight - knee, ankle - knee);
                    straightest = Mathf.Min(straightest, bend);
                    most = Mathf.Max(most, bend);
                }
                Assert.That(straightest, Is.InRange(24f, 35f), $"{kind}: knie onderin");
                Assert.That(most, Is.InRange(100f, 120f), $"{kind}: knie bovenin");
            }
        }

        [Test]
        public void Arms_ReachTheHoods()
        {
            var g = new CyclistGeometry(BikeKind.Allround);
            float reach = Vector3.Distance(g.ShoulderRight, g.HoodRight);
            Assert.Less(reach, g.UpperArm + g.Forearm, "armen licht gebogen, niet overstrekt");
            Assert.Greater(reach, 0.85f * (g.UpperArm + g.Forearm));
            Assert.AreEqual(0.99f, g.FrontAxle.z - g.RearAxle.z, 1e-3f, "wielbasis 99 cm");
        }
    }
}
