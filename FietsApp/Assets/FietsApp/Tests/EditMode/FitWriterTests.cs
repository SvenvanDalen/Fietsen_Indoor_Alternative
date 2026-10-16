using System;
using System.Text;
using FietsApp.Recording;
using NUnit.Framework;

namespace FietsApp.Tests
{
    // Referentiewaarden los uitgerekend (Python) of uit de standaard; het hele bestand is daarnaast
    // gecontroleerd met de onafhankelijke FIT-lezer fitdecode (zie PR van story 5.1).
    public class FitWriterTests
    {
        static readonly DateTime T0 = new DateTime(2026, 10, 16, 8, 0, 0, DateTimeKind.Utc);

        static RideLog Ride(int seconds)
        {
            var log = new RideLog();
            for (int i = 0; i < seconds; i++)
                log.Add(new RideSample(T0.AddSeconds(i), 200f + i, 85f, 140f, 5f, i * 5f, 300f + i * 0.4f, 44.1739, 5.27843));
            return log;
        }

        [Test]
        public void Crc_MatchesTheStandardCheckValue()
        {
            // CRC-16/ARC van "123456789" is 0xBB3D.
            Assert.That(FitCrc.Compute(Encoding.ASCII.GetBytes("123456789")), Is.EqualTo(0xBB3D));
        }

        [Test]
        public void FitTime_CountsFromEndOf1989()
        {
            Assert.That(FitWriter.ToFitTime(T0), Is.EqualTo(1161072000u));
        }

        [Test]
        public void Semicircles()
        {
            Assert.That(FitWriter.ToSemicircles(45.0), Is.EqualTo(536870912)); // 2^29
            Assert.That(FitWriter.ToSemicircles(44.1739), Is.EqualTo(527015155));
            Assert.That(FitWriter.ToSemicircles(5.27843), Is.EqualTo(62974123));
        }

        [Test]
        public void Header_DescribesTheFile()
        {
            var bytes = FitWriter.Write(Ride(10));

            Assert.That(bytes[0], Is.EqualTo(14));                      // kopgrootte
            Assert.That(bytes[1], Is.EqualTo(0x20));                    // protocol 2.0
            Assert.That(BitConverter.ToUInt16(bytes, 2), Is.EqualTo(2132));
            Assert.That(BitConverter.ToUInt32(bytes, 4), Is.EqualTo((uint)(bytes.Length - 14 - 2)));
            Assert.That(Encoding.ASCII.GetString(bytes, 8, 4), Is.EqualTo(".FIT"));
        }

        [Test]
        public void Crcs_AreValid()
        {
            var bytes = FitWriter.Write(Ride(10));

            // Een CRC over gegevens plus hun eigen CRC geeft 0: zo controleert een lezer het bestand.
            Assert.That(FitCrc.Compute(bytes[..14]), Is.EqualTo(0));
            Assert.That(FitCrc.Compute(bytes), Is.EqualTo(0));
        }

        [Test]
        public void GrowsWithOneRecordPerSecond()
        {
            // Elk record: 1 kopbyte + 4+4+4+2+1+1+4+2+2 = 25 bytes.
            int ten = FitWriter.Write(Ride(10)).Length;
            int eleven = FitWriter.Write(Ride(11)).Length;

            Assert.That(eleven - ten, Is.EqualTo(25));
        }

        [Test]
        public void EmptyRide_IsRefused()
        {
            Assert.Throws<ArgumentException>(() => FitWriter.Write(new RideLog()));
        }
    }

    public class RideLogTests
    {
        static readonly DateTime T0 = new DateTime(2026, 10, 16, 8, 0, 0, DateTimeKind.Utc);

        [Test]
        public void Summary()
        {
            var log = new RideLog();
            log.Add(new RideSample(T0, 200f, 80f, 0f, 5f, 0f, 300f));
            log.Add(new RideSample(T0.AddSeconds(1), 300f, 90f, 150f, 5f, 5f, 302f));
            log.Add(new RideSample(T0.AddSeconds(10), 250f, 85f, 160f, 5f, 10f, 301f)); // na een pauze

            Assert.That(log.AveragePower, Is.EqualTo(250f));
            Assert.That(log.MaxPower, Is.EqualTo(300f));
            Assert.That(log.AverageHeartRate, Is.EqualTo(155f)); // zonder de 0 (geen band)
            Assert.That(log.Ascent, Is.EqualTo(2f));
            Assert.That(log.DistanceM, Is.EqualTo(10f));
            Assert.That(log.TimerSeconds, Is.EqualTo(3));
            Assert.That(log.ElapsedSeconds, Is.EqualTo(11));
        }
    }
}
