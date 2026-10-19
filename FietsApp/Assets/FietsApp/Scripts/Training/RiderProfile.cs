using System.Collections.Generic;

namespace FietsApp.Training
{
    /// <summary>
    /// Het lokale rijdersprofiel (stories 8.1, 8.5, 2.3 en 9.2): FTP voor zones en workouts, en per klim de
    /// weerstandsgevoeligheid en de beste tijd. Velden in kleine letters, zodat <c>JsonUtility</c> het kan opslaan (die kent geen
    /// dictionary, vandaar de lijst); accounts en een profiel in de cloud volgen bij E7.
    /// </summary>
    [System.Serializable]
    public sealed class RiderProfile
    {
        public const int DefaultFtpWatts = 200;
        public const int MinGradeScale = 50, MaxGradeScale = 150, DefaultGradeScale = 100;

        /// <summary>Functional Threshold Power in watt; wordt bijgewerkt door de FTP-test (8.1).</summary>
        public int ftpWatts = DefaultFtpWatts;

        /// <summary>Datum van de laatste FTP-test (jjjj-mm-dd), leeg als de FTP nog de standaardwaarde is.</summary>
        public string ftpTestedOn = "";

        /// <summary>Weerstandsgevoeligheid per klim (2.3); klimmen die er niet in staan rijden op 100%.</summary>
        public List<ClimbSetting> climbs = new List<ClimbSetting>();

        /// <summary>Welk deel van de echte helling de trainer laat voelen, in procent (50-150).</summary>
        public int GradeScale(string climb)
        {
            foreach (var c in climbs)
                if (c.climb == climb) return Clamp(c.gradeScalePercent);
            return DefaultGradeScale;
        }

        public void SetGradeScale(string climb, int percent)
        {
            percent = Clamp(percent);
            climbs.RemoveAll(c => c.climb == climb);
            if (percent != DefaultGradeScale) climbs.Add(new ClimbSetting { climb = climb, gradeScalePercent = percent });
        }

        /// <summary>Beste en laatste klimtijd per klim (9.2).</summary>
        public List<ClimbRecord> records = new List<ClimbRecord>();

        /// <summary>Het record van een klim, of null als je hem nog niet hebt uitgereden.</summary>
        public ClimbRecord Record(string climb)
        {
            foreach (var r in records)
                if (r.climb == climb) return r;
            return null;
        }

        /// <summary>Legt een klimtijd vast (van voet tot top) en geeft terug hoe die zich verhoudt tot record en vorige poging.</summary>
        public ClimbResult AddClimbTime(string climb, double seconds, string date)
        {
            var record = Record(climb);
            if (record == null)
            {
                records.Add(new ClimbRecord
                {
                    climb = climb, firstOn = date, bestSeconds = seconds, bestOn = date, lastSeconds = seconds, lastOn = date, attempts = 1,
                });
                return new ClimbResult(seconds, null, null);
            }
            var result = new ClimbResult(seconds, record.bestSeconds, record.lastSeconds);
            if (result.IsRecord)
            {
                record.bestSeconds = seconds;
                record.bestOn = date;
            }
            record.lastSeconds = seconds;
            record.lastOn = date;
            record.attempts++;
            return result;
        }

        /// <summary>Badge van een klim (9.1): je hebt hem van voet tot top gereden.</summary>
        public bool HasBadge(string climb) => Record(climb) != null;

        /// <summary>Hoeveel van deze klimmen je al hebt uitgereden.</summary>
        public int BadgeCount(IEnumerable<string> climbs)
        {
            int n = 0;
            foreach (var c in climbs)
                if (HasBadge(c)) n++;
            return n;
        }

        static int Clamp(int p) => p < MinGradeScale ? MinGradeScale : p > MaxGradeScale ? MaxGradeScale : p;
    }

    [System.Serializable]
    public sealed class ClimbSetting
    {
        public string climb = "";
        public int gradeScalePercent = RiderProfile.DefaultGradeScale;
    }
}
