using System;

namespace FietsApp.Trainers
{
    /// <summary>
    /// Trainer en sensoren via FietsBridge (Bluetooth). Krijgt meetwaarden van één of meer
    /// apparaten (bijvoorbeeld trainer plus hartslagband) en voegt ze samen.
    /// Blijven metingen uit, dan vallen de waarden terug op 0, zodat de rijder niet doorrijdt
    /// op een oude meting.
    /// </summary>
    public sealed class BridgeTrainer : ITrainer
    {
        const double PowerTimeout = 3.0;     // s
        const double HeartRateTimeout = 5.0; // s

        double _lastPowerTime = double.NegativeInfinity;
        double _lastCadenceTime = double.NegativeInfinity;
        double _lastHeartRateTime = double.NegativeInfinity;
        float _gradePercent;

        public string Name { get; set; } = "Bluetooth";
        public bool IsConnected { get; set; }

        public float PowerWatts { get; private set; }
        public float CadenceRpm { get; private set; }
        public float HeartRateBpm { get; private set; }

        public float MaxGradePercent { get; }
        public float GradePercent => _gradePercent;

        /// <summary>De (begrensde) helling is veranderd en moet naar de trainer (story 1.2).</summary>
        public event Action<float> GradeChanged;

        public BridgeTrainer(float maxGradePercent = 20f)
        {
            MaxGradePercent = maxGradePercent;
        }

        public void SetGrade(float gradePercent)
        {
            float clamped = gradePercent < -MaxGradePercent ? -MaxGradePercent
                : gradePercent > MaxGradePercent ? MaxGradePercent : gradePercent;
            if (clamped == _gradePercent) return;
            _gradePercent = clamped;
            GradeChanged?.Invoke(clamped);
        }

        /// <summary>
        /// Verwerk een meting. -1 betekent "niet meegestuurd" (afspraak met de brug);
        /// <paramref name="time"/> is een oplopende klok in seconden.
        /// </summary>
        public void Apply(float power, float cadence, float heartRate, double time)
        {
            if (power != -1f)
            {
                PowerWatts = power > 0f ? power : 0f;
                _lastPowerTime = time;
            }
            if (cadence >= 0f)
            {
                CadenceRpm = cadence;
                _lastCadenceTime = time;
            }
            if (heartRate >= 0f)
            {
                HeartRateBpm = heartRate;
                _lastHeartRateTime = time;
            }
        }

        /// <summary>Zet verouderde waarden op 0; aanroepen per frame.</summary>
        public void Tick(double time)
        {
            if (time - _lastPowerTime > PowerTimeout) PowerWatts = 0f;
            if (time - _lastCadenceTime > PowerTimeout) CadenceRpm = 0f;
            if (time - _lastHeartRateTime > HeartRateTimeout) HeartRateBpm = 0f;
        }
    }
}
