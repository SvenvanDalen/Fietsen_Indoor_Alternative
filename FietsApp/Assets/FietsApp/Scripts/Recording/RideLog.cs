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
