using System;
using System.Collections.Generic;
using System.Linq;
using FietsApp.World;
using NUnit.Framework;
using UnityEngine;

namespace FietsApp.Tests
{
    public class RouteMarkersTests
    {
        /// <summary>Rechte route naar het noorden, <paramref name="length"/> m lang met een vaste helling.</summary>
        static Route Straight(float length, float gradePercent)
        {
            var pts = new List<RoutePoint>();
            for (float d = 0; d <= length + 0.01f; d += 10f) pts.Add(new RoutePoint(d, 0f, d, d * gradePercent / 100f));
            return new Route(pts);
        }

        /// <summary>Zigzag: rechte stukken van 400 m met daartussen <paramref name="turns"/> keerbochten (halve cirkels van 15 m).</summary>
        static Route Zigzag(int turns)
        {
            var pts = new List<RoutePoint>();
            float x = 0f, z = 0f, d = 0f, dir = 1f;
            void Add() => pts.Add(new RoutePoint(d, x, z, d * 0.08f));
            Add();
            for (int leg = 0; leg <= turns; leg++)
            {
                for (int i = 0; i < 40; i++) { z += dir * 10f; d += 10f; Add(); }
                if (leg == turns) break;
                // Halve cirkel naar rechts (x + 30), straal 15 m, in 12 stukjes.
                float cx = x + 15f, cz = z;
                for (int i = 1; i <= 12; i++)
                {
                    double a = Math.PI - Math.PI * i / 12; // van links (x - 15 t.o.v. midden) naar rechts
                    float nx = cx + 15f * (float)Math.Cos(a);
                    float nz = cz + dir * 15f * (float)Math.Sin(a);
                    d += Mathf.Sqrt((nx - x) * (nx - x) + (nz - z) * (nz - z));
                    x = nx;
                    z = nz;
                    Add();
                }
                dir = -dir;
            }
            return new Route(pts);
        }

        [Test]
        public void KilometerMarkersCountDownToTheTop()
        {
            var markers = RouteMarkers.Kilometers(Straight(3500f, 5f));
            Assert.That(markers.Select(m => m.KmToTop), Is.EqualTo(new[] { 3, 2, 1 }));
            Assert.That(markers.Select(m => m.Distance), Is.EqualTo(new[] { 500f, 1500f, 2500f }));
            Assert.That(markers, Has.All.Matches<KilometerMarker>(m => Math.Abs(m.NextKmGradePercent - 5f) < 0.01f));
        }

        [Test]
        public void ShortClimbHasNoKilometerMarkers()
        {
            Assert.That(RouteMarkers.Kilometers(Straight(800f, 10f)), Is.Empty);
        }

        [Test]
        public void ZigzagHairpinsAreFoundAndNumberedTowardsTheTop()
        {
            var pins = RouteMarkers.Hairpins(Zigzag(3));
            Assert.That(pins.Select(p => p.Number), Is.EqualTo(new[] { 3, 2, 1 }));
            // Elke bocht ligt na 400 m recht plus de vorige bochten (halve cirkel ≈ 47 m); het midden van de bocht.
            Assert.That(pins[0].Distance, Is.EqualTo(400f + 23.5f).Within(20f));
            Assert.That(pins.Select(p => Math.Abs(p.TurnDegrees)), Has.All.GreaterThan(150f));
        }

        [Test]
        public void StraightRoadHasNoHairpins()
        {
            Assert.That(RouteMarkers.Hairpins(Straight(5000f, 7f)), Is.Empty);
        }

        [Test]
        public void AlpeDHuezHasItsTwentyOneBends()
        {
            // Werkelijkheid: 21 genummerde bochten (bord 21 onderaan, 1 bij de finish).
            var pins = RouteMarkers.Hairpins(TestPackages.Route("AlpeDHuez"));
            Assert.That(pins, Has.Count.EqualTo(21));
            Assert.That(pins[0].Number, Is.EqualTo(21));
        }

        [Test]
        public void StelvioFromPratoHasAboutFortyEightTornanti()
        {
            // Werkelijkheid: 48 tornanti; de bovenste liggen soms zo dicht op elkaar dat er een paar samenvallen.
            var pins = RouteMarkers.Hairpins(TestPackages.Route("Stelvio_Prato"));
            Assert.That(pins.Count, Is.InRange(44, 50));
        }
    }
}
