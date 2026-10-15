using System;

namespace FietsApp.Trainers
{
    /// <summary>
    /// Trainer zonder hardware (story 1.6). Het doelvermogen wordt van buitenaf gezet
    /// (toetsenbord of test); cadans en hartslag volgen er geleidelijk uit.
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

        /// <summary>Werkt de gesimuleerde metingen bij; aanroepen per frame.</summary>
        public void Tick(float dt)
        {
            PowerWatts = _targetPower;
            CadenceRpm = _targetPower > 0f ? 85f : 0f;

            float targetHr = RestingHeartRate + HeartRatePerWatt * _targetPower;
            float alpha = 1f - (float)Math.Exp(-dt / HeartRateTimeConstant);
            HeartRateBpm += (targetHr - HeartRateBpm) * alpha;
        }

        static float Clamp(float v, float min, float max) => v < min ? min : v > max ? max : v;
    }
}
