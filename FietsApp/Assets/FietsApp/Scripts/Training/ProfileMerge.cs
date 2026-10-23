using System;
using System.Collections.Generic;
using System.Linq;

namespace FietsApp.Training
{
    /// <summary>
    /// Voegt twee versies van het profiel samen, bijvoorbeeld van de laptop en uit de Gist (story 7.2). Instellingen (FTP,
    /// gewicht, max hartslag, weerstand per klim, Everesting) komen uit de laatst gewijzigde versie. Klimtijden gaan
    /// nooit verloren: per klim de snelste tijd, de laatste poging en de vroegste badge uit beide.
    /// </summary>
    public static class ProfileMerge
    {
        public static RiderProfile Merge(RiderProfile local, RiderProfile remote)
        {
            if (remote == null) return local;
            if (local == null) return remote;
            bool localNewer = string.CompareOrdinal(local.updatedAt ?? "", remote.updatedAt ?? "") >= 0;
            var newer = localNewer ? local : remote;
            var older = localNewer ? remote : local;

            var result = new RiderProfile
            {
                updatedAt = newer.updatedAt,
                ftpWatts = newer.ftpWatts,
                ftpTestedOn = newer.ftpTestedOn,
                weightKg = newer.weightKg,
                maxHeartRateBpm = newer.maxHeartRateBpm,
                climbs = newer.climbs.Select(c => new ClimbSetting { climb = c.climb, gradeScalePercent = c.gradeScalePercent }).ToList(),
                everesting = Copy(newer.everesting ?? new EverestingChallenge()),
            };

            var names = newer.records.Select(r => r.climb).Concat(older.records.Select(r => r.climb)).Distinct();
            foreach (var name in names)
                result.records.Add(Merge(newer.Record(name), older.Record(name)));
            return result;
        }

        /// <summary>Eén klim: snelste tijd, laatste poging, vroegste eerste keer, meeste pogingen.</summary>
        static ClimbRecord Merge(ClimbRecord a, ClimbRecord b)
        {
            if (b == null) return Copy(a);
            if (a == null) return Copy(b);
            var best = a.bestSeconds <= b.bestSeconds ? a : b;
            var last = string.CompareOrdinal(b.lastOn ?? "", a.lastOn ?? "") > 0 ? b : a;
            string first = new[] { a.firstOn, b.firstOn }.Where(d => !string.IsNullOrEmpty(d)).OrderBy(d => d, StringComparer.Ordinal).FirstOrDefault() ?? "";
            return new ClimbRecord
            {
                climb = a.climb,
                firstOn = first,
                bestSeconds = best.bestSeconds,
                bestOn = best.bestOn,
                lastSeconds = last.lastSeconds,
                lastOn = last.lastOn,
                attempts = Math.Max(a.attempts, b.attempts),
            };
        }

        static ClimbRecord Copy(ClimbRecord r) => new ClimbRecord
        {
            climb = r.climb, firstOn = r.firstOn, bestSeconds = r.bestSeconds, bestOn = r.bestOn,
            lastSeconds = r.lastSeconds, lastOn = r.lastOn, attempts = r.attempts,
        };

        static EverestingChallenge Copy(EverestingChallenge e) => new EverestingChallenge
        {
            active = e.active, startedOn = e.startedOn, climbedMeters = e.climbedMeters, rides = e.rides, completedOn = e.completedOn,
        };
    }
}
