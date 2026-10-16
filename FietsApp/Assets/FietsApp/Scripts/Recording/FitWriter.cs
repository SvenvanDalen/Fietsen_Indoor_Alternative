using System;
using System.IO;

namespace FietsApp.Recording
{
    /// <summary>
    /// Schrijft een rit als FIT-activiteit (story 5.1), het formaat dat Strava, Garmin Connect en Intervals.icu inlezen.
    /// Berichten volgens het FIT-profiel van Garmin: file_id, event (start), record per seconde, event (stop), lap,
    /// session (fietsen, virtueel) en activity. Alles little-endian; schalen en ongeldige waarden zoals in het profiel.
    /// </summary>
    public static class FitWriter
    {
        // FIT-tijd telt in seconden vanaf 31-12-1989 00:00 UTC.
        static readonly DateTime FitEpoch = new DateTime(1989, 12, 31, 0, 0, 0, DateTimeKind.Utc);
        const ushort ProfileVersion = 2132; // 21.32
        const byte ProtocolVersion = 0x20;  // 2.0

        // Basistypen (FIT-profiel, "fit_base_type").
        const byte Enum = 0x00, UInt8 = 0x02, UInt16 = 0x84, SInt32 = 0x85, UInt32 = 0x86, UInt32Z = 0x8C;

        // Globale berichtnummers.
        const ushort FileIdMesg = 0, SessionMesg = 18, LapMesg = 19, RecordMesg = 20, EventMesg = 21, ActivityMesg = 34;

        // Lokale berichttypen in dit bestand.
        const byte LocalFileId = 0, LocalEvent = 1, LocalRecord = 2, LocalLap = 3, LocalSession = 4, LocalActivity = 5;

        // Enumwaarden uit het profiel.
        const byte FileTypeActivity = 4, EventTimer = 0, EventSession = 8, EventLap = 9, EventActivity = 26;
        const byte EventTypeStart = 0, EventTypeStop = 1, EventTypeStopAll = 4;
        const byte SportCycling = 2, SubSportVirtualActivity = 58;
        const ushort ManufacturerDevelopment = 255;

        public static uint ToFitTime(DateTime utc) => (uint)(utc - FitEpoch).TotalSeconds;

        /// <summary>Graden naar "semicircles": 2^31 staat voor 180 graden.</summary>
        public static int ToSemicircles(double degrees) => (int)Math.Round(degrees * (2147483648.0 / 180.0));

        public static byte[] Write(RideLog log)
        {
            if (log.Count == 0) throw new ArgumentException("Een rit zonder metingen kan niet worden opgeslagen.", nameof(log));

            var body = new MemoryStream();
            var w = new BinaryWriter(body);
            uint start = ToFitTime(log.StartUtc);
            uint end = ToFitTime(log.EndUtc);
            uint elapsed = (uint)Math.Round(log.ElapsedSeconds * 1000.0);
            uint timer = (uint)Math.Round(log.TimerSeconds * 1000.0);
            uint distance = Centimeters(log.DistanceM);

            Define(w, LocalFileId, FileIdMesg, (0, 1, Enum), (1, 2, UInt16), (2, 2, UInt16), (3, 4, UInt32Z), (4, 4, UInt32));
            w.Write(LocalFileId);
            w.Write(FileTypeActivity);
            w.Write(ManufacturerDevelopment);
            w.Write((ushort)1);   // product
            w.Write(1u);          // serienummer
            w.Write(start);       // time_created

            Define(w, LocalEvent, EventMesg, (253, 4, UInt32), (0, 1, Enum), (1, 1, Enum));
            WriteEvent(w, start, EventTimer, EventTypeStart);

            Define(w, LocalRecord, RecordMesg,
                (253, 4, UInt32), (0, 4, SInt32), (1, 4, SInt32), (2, 2, UInt16), (3, 1, UInt8), (4, 1, UInt8),
                (5, 4, UInt32), (6, 2, UInt16), (7, 2, UInt16));
            foreach (var s in log.Samples)
            {
                w.Write(LocalRecord);
                w.Write(ToFitTime(s.TimeUtc));
                w.Write(s.HasPosition ? ToSemicircles(s.Latitude) : int.MaxValue);   // position_lat
                w.Write(s.HasPosition ? ToSemicircles(s.Longitude) : int.MaxValue);  // position_long
                w.Write(Scaled16(s.Elevation, 5f, 500f));                          // altitude: m × 5 + 500
                w.Write(Byte(s.HeartRateBpm));                                     // heart_rate
                w.Write(Byte(s.CadenceRpm));                                       // cadence
                w.Write(Centimeters(s.DistanceM));                                 // distance: m × 100
                w.Write(Scaled16(s.SpeedMs, 1000f, 0f));                           // speed: m/s × 1000
                w.Write(Scaled16(s.PowerWatts, 1f, 0f));                           // power
            }

            WriteEvent(w, end, EventTimer, EventTypeStopAll);

            Define(w, LocalLap, LapMesg, (253, 4, UInt32), (2, 4, UInt32), (7, 4, UInt32), (8, 4, UInt32), (9, 4, UInt32),
                (0, 1, Enum), (1, 1, Enum));
            w.Write(LocalLap);
            w.Write(end);
            w.Write(start);
            w.Write(elapsed);
            w.Write(timer);
            w.Write(distance);
            w.Write(EventLap);
            w.Write(EventTypeStop);

            Define(w, LocalSession, SessionMesg,
                (253, 4, UInt32), (2, 4, UInt32), (7, 4, UInt32), (8, 4, UInt32), (9, 4, UInt32), (5, 1, Enum), (6, 1, Enum),
                (20, 2, UInt16), (21, 2, UInt16), (16, 1, UInt8), (17, 1, UInt8), (18, 1, UInt8), (22, 2, UInt16),
                (25, 2, UInt16), (26, 2, UInt16), (0, 1, Enum), (1, 1, Enum));
            w.Write(LocalSession);
            w.Write(end);
            w.Write(start);
            w.Write(elapsed);
            w.Write(timer);
            w.Write(distance);
            w.Write(SportCycling);
            w.Write(SubSportVirtualActivity);
            w.Write(Scaled16(log.AveragePower, 1f, 0f));
            w.Write(Scaled16(log.MaxPower, 1f, 0f));
            w.Write(Byte(log.AverageHeartRate));
            w.Write(Byte(log.MaxHeartRate));
            w.Write(Byte(log.AverageCadence));
            w.Write(Scaled16(log.Ascent, 1f, 0f));
            w.Write((ushort)0); // first_lap_index
            w.Write((ushort)1); // num_laps
            w.Write(EventSession);
            w.Write(EventTypeStop);

            Define(w, LocalActivity, ActivityMesg, (253, 4, UInt32), (0, 4, UInt32), (1, 2, UInt16), (2, 1, Enum),
                (3, 1, Enum), (4, 1, Enum), (5, 4, UInt32));
            w.Write(LocalActivity);
            w.Write(end);
            w.Write(timer);
            w.Write((ushort)1);  // num_sessions
            w.Write((byte)0);    // type: handmatig
            w.Write(EventActivity);
            w.Write(EventTypeStop);
            w.Write((uint)(end + (long)TimeZoneInfo.Local.GetUtcOffset(log.EndUtc).TotalSeconds)); // local_timestamp

            w.Flush();
            var data = body.ToArray();

            var file = new MemoryStream();
            var f = new BinaryWriter(file);
            var header = new byte[12];
            header[0] = 14;
            header[1] = ProtocolVersion;
            BitConverter.GetBytes(ProfileVersion).CopyTo(header, 2);
            BitConverter.GetBytes((uint)data.Length).CopyTo(header, 4);
            header[8] = (byte)'.'; header[9] = (byte)'F'; header[10] = (byte)'I'; header[11] = (byte)'T';
            f.Write(header);
            f.Write(FitCrc.Compute(header));
            f.Write(data);
            f.Flush();
            f.Write(FitCrc.Compute(file.ToArray()));
            f.Flush();
            return file.ToArray();
        }

        static void WriteEvent(BinaryWriter w, uint time, byte evt, byte type)
        {
            w.Write(LocalEvent);
            w.Write(time);
            w.Write(evt);
            w.Write(type);
        }

        /// <summary>Definitiebericht: welke velden (nummer, grootte, basistype) een lokaal berichttype heeft.</summary>
        static void Define(BinaryWriter w, byte local, ushort global, params (byte number, byte size, byte type)[] fields)
        {
            w.Write((byte)(0x40 | local));
            w.Write((byte)0); // gereserveerd
            w.Write((byte)0); // little-endian
            w.Write(global);
            w.Write((byte)fields.Length);
            foreach (var (number, size, type) in fields)
            {
                w.Write(number);
                w.Write(size);
                w.Write(type);
            }
        }

        static byte Byte(float value) => value > 0f && value < 255f ? (byte)Math.Round(value) : (byte)0xFF;

        static ushort Scaled16(float value, float scale, float offset)
        {
            double v = Math.Round((value + offset) * scale);
            return v >= 0 && v < ushort.MaxValue ? (ushort)v : ushort.MaxValue;
        }

        static uint Centimeters(float meters) => (uint)Math.Max(0, Math.Round(meters * 100.0));
    }

    /// <summary>De CRC-16 van FIT (dezelfde als CRC-16/ARC, polynoom 0xA001), per halve byte uit een tabel.</summary>
    public static class FitCrc
    {
        static readonly ushort[] Table =
        {
            0x0000, 0xCC01, 0xD801, 0x1400, 0xF001, 0x3C00, 0x2800, 0xE401,
            0xA001, 0x6C00, 0x7800, 0xB401, 0x5000, 0x9C01, 0x8801, 0x4400,
        };

        public static ushort Compute(byte[] data)
        {
            ushort crc = 0;
            foreach (byte b in data) crc = Update(crc, b);
            return crc;
        }

        public static ushort Update(ushort crc, byte b)
        {
            ushort tmp = Table[crc & 0xF];
            crc = (ushort)((crc >> 4) & 0x0FFF);
            crc = (ushort)(crc ^ tmp ^ Table[b & 0xF]);
            tmp = Table[crc & 0xF];
            crc = (ushort)((crc >> 4) & 0x0FFF);
            return (ushort)(crc ^ tmp ^ Table[(b >> 4) & 0xF]);
        }
    }
}
