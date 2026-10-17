using System;

namespace FietsApp.Trainers
{
    /// <summary>
    /// Trainer zonder hardware (story 1.6). Het doelvermogen wordt van buitenaf gezet
    /// (toetsenbord of test); cadans en hartslag volgen er geleidelijk uit. In ERG-modus (story 8.2)
    /// levert de gesimuleerde rijder precies het ERG-vermogen, zodat workouts zonder trainer te testen zijn.
    /// </summary>
    public sealed class SimulatorTrainer : ITrainer
    {
        const float RestingHeartRate = 60f;
        const float HeartRatePerWatt = 0.3f;
        const float HeartRateTimeConstant = 20f; // s, hoe traag de hartslag reageert

        float _targetPower;
        float _gradePercent;

        public string Name => "Simulator";
        public bool IsConnected => true;

        public float PowerWatts { get; private set; }
        public float CadenceRpm { get; private set; }
        public float HeartRateBpm { get; private set; } = RestingHeartRate;

        public float MaxGradePercent { get; }
        public float GradePercent => _gradePercent;

        public float MaxPowerWatts { get; }

        public SimulatorTrainer(float maxGradePercent = 20f, float maxPowerWatts = 1200f)
        {
            MaxGradePercent = maxGradePercent;
            MaxPowerWatts = maxPowerWatts;
        }

        public float TargetPowerWatts
        {
            get => _targetPower;
            set => _targetPower = Clamp(value, 0f, MaxPowerWatts);
        }

        public void SetGrade(float gradePercent) =>
            _gradePercent = Clamp(gradePercent, -MaxGradePercent, MaxGradePercent);

        public float ErgWatts { get; private set; }

        public void SetErgWatts(float watts) => ErgWatts = Clamp(watts, 0f, MaxPowerWatts);

        /// <summary>Werkt de gesimuleerde metingen bij; aanroepen per frame.</summary>
        public void Tick(float dt)
        {
            PowerWatts = ErgWatts > 0f ? ErgWatts : _targetPower;
            CadenceRpm = PowerWatts > 0f ? 85f : 0f;

            float targetHr = RestingHeartRate + HeartRatePerWatt * PowerWatts;
            float alpha = 1f - (float)Math.Exp(-dt / HeartRateTimeConstant);
            HeartRateBpm += (targetHr - HeartRateBpm) * alpha;
        }

        static float Clamp(float v, float min, float max) => v < min ? min : v > max ? max : v;
    }
}
