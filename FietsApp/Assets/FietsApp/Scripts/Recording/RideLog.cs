using System;
using System.Collections.Generic;

namespace FietsApp.Recording
{
    /// <summary>Eén meting per seconde tijdens de rit (story 5.1).</summary>
    public readonly struct RideSample
    {
        public readonly DateTime TimeUtc;
        public readonly float PowerWatts, CadenceRpm, HeartRateBpm, SpeedMs, DistanceM, Elevation;
        public readonly double Latitude, Longitude; // NaN als de rit geen echte plek heeft

        public RideSample(DateTime timeUtc, float powerWatts, float cadenceRpm, float heartRateBpm, float speedMs,
            float distanceM, float elevation, double latitude = double.NaN, double longitude = double.NaN)
        {
            TimeUtc = timeUtc;
            PowerWatts = powerWatts;
            CadenceRpm = cadenceRpm;
            HeartRateBpm = heartRateBpm;
            SpeedMs = speedMs;
            DistanceM = distanceM;
            Elevation = elevation;
            Latitude = latitude;
            Longitude = longitude;
        }

        public bool HasPosition => !double.IsNaN(Latitude) && !double.IsNaN(Longitude);
    }

    /// <summary>De metingen van één rit, met de samenvatting die in het .fit-bestand komt.</summary>
    public sealed class RideLog
    {
        readonly List<RideSample> _samples = new List<RideSample>();

        public IReadOnlyList<RideSample> Samples => _samples;
        public int Count => _samples.Count;

        public void Add(RideSample sample) => _samples.Add(sample);

        public DateTime StartUtc => _samples[0].TimeUtc;
        public DateTime EndUtc => _samples[_samples.Count - 1].TimeUtc;

        /// <summary>Tijd van eerste tot laatste meting plus die laatste seconde, inclusief pauzes.</summary>
        public double ElapsedSeconds => (EndUtc - StartUtc).TotalSeconds + 1.0;

        /// <summary>Gereden tijd: één seconde per meting, zonder pauzes.</summary>
        public double TimerSeconds => _samples.Count;

        public float DistanceM => _samples[_samples.Count - 1].DistanceM - _samples[0].DistanceM;

        public float AveragePower => Average(s => s.PowerWatts);
        public float MaxPower => Max(s => s.PowerWatts);
        public float AverageCadence => Average(s => s.CadenceRpm);
        public float AverageHeartRate => Average(s => s.HeartRateBpm, skipZero: true);
        public float MaxHeartRate => Max(s => s.HeartRateBpm);

        /// <summary>Opgetelde stijging in meters.</summary>
        public float Ascent
        {
            get
            {
                float total = 0f;
                for (int i = 1; i < _samples.Count; i++)
                    total += Math.Max(0f, _samples[i].Elevation - _samples[i - 1].Elevation);
                return total;
            }
        }

        /// <summary>
        /// Gereden tijd (s, zonder pauzes) waarop de rit afstand <paramref name="distance"/> passeerde, tussen twee metingen
        /// in geïnterpoleerd; 0 als de rit daar al voorbij begon, null als hij er (nog) niet kwam.
        /// </summary>
        public double? TimerAtDistance(float distance)
        {
            if (_samples.Count == 0) return null;
            if (_samples[0].DistanceM >= distance) return 0.0;
            for (int i = 1; i < _samples.Count; i++)
            {
                float a = _samples[i - 1].DistanceM, b = _samples[i].DistanceM;
                if (b < distance) continue;
                return i - 1 + (b > a ? (distance - a) / (double)(b - a) : 1.0);
            }
            return null;
        }

        /// <summary>Afstand op gereden tijd <paramref name="timer"/> (s), tussen twee metingen in geïnterpoleerd en begrensd op de opname.</summary>
        public float DistanceAtTimer(double timer)
        {
            if (_samples.Count == 0) return 0f;
            if (timer <= 0) return _samples[0].DistanceM;
            int i = (int)timer;
            if (i >= _samples.Count - 1) return _samples[_samples.Count - 1].DistanceM;
            double t = timer - i;
            return (float)(_samples[i].DistanceM + (_samples[i + 1].DistanceM - _samples[i].DistanceM) * t);
        }

        /// <summary>Hoe ver na de voet de eerste meting mag liggen om nog als hele klim te tellen (de eerste meting komt na een seconde).</summary>
        public const float StartTolerance = 25f;

        /// <summary>
        /// Klimtijd (s) van de voet op <paramref name="start"/> tot de top op <paramref name="end"/>; null zolang de top
        /// niet gehaald is, of als de rit pas halverwege de klim begon.
        /// </summary>
        public double? SecondsBetween(float start, float end)
        {
            if (_samples.Count == 0 || _samples[0].DistanceM > start + StartTolerance) return null;
            var from = TimerAtDistance(start);
            var to = TimerAtDistance(end);
            return from.HasValue && to.HasValue ? to.Value - from.Value : (double?)null;
        }

        /// <summary>Gemiddeld vermogen over de metingen tussen twee afstanden (bijvoorbeeld op de klim).</summary>
        public float AveragePowerBetween(float start, float end)
        {
            double sum = 0;
            int n = 0;
            foreach (var s in _samples)
            {
                if (s.DistanceM < start || s.DistanceM > end) continue;
                sum += s.PowerWatts;
                n++;
            }
            return n > 0 ? (float)(sum / n) : 0f;
        }

        float Average(Func<RideSample, float> value, bool skipZero = false)
        {
            double sum = 0;
            int n = 0;
            foreach (var s in _samples)
            {
                float v = value(s);
                if (skipZero && v <= 0f) continue;
                sum += v;
                n++;
            }
            return n > 0 ? (float)(sum / n) : 0f;
        }

        float Max(Func<RideSample, float> value)
        {
            float max = 0f;
            foreach (var s in _samples) max = Math.Max(max, value(s));
            return max;
        }
    }
}
