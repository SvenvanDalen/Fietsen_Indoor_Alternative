using FietsApp.Physics;
using FietsApp.Trainers;
using UnityEngine;

namespace FietsApp
{
    /// <summary>
    /// Koppelt een trainer aan het fysicamodel: leest vermogen, berekent snelheid en afstand,
    /// en geeft de helling door aan de trainer.
    /// </summary>
    public sealed class RideController : MonoBehaviour
    {
        [SerializeField, Min(30f)] float totalMassKg = 80f;
        [SerializeField, Range(0.002f, 0.02f)] float rollingResistance = 0.004f;
        [SerializeField, Range(0.15f, 0.6f)] float dragAreaM2 = 0.32f;

        RiderPhysics _physics;
        float _gradePercent;

        public ITrainer Trainer { get; private set; }
        public SimulatorTrainer Simulator { get; private set; }

        public float SpeedMs { get; private set; }
        public float DistanceM { get; private set; }
        public float ElapsedSeconds { get; private set; }

        /// <summary>Remmen, van 0 (los) tot 1 (vol); bijvoorbeeld de spatiebalk. Werkt met elke trainer.</summary>
        public float Brake { get; set; }

        /// <summary>Helling van de route. De trainer krijgt dezelfde waarde, begrensd op zijn maximum.</summary>
        public float GradePercent
        {
            get => _gradePercent;
            set
            {
                _gradePercent = value;
                Trainer?.SetGrade(value);
            }
        }

        void Awake()
        {
            _physics = new RiderPhysics(CurrentParameters());
            Simulator = new SimulatorTrainer();
            UseTrainer(Simulator);
        }

        void OnValidate()
        {
            if (_physics != null) _physics.Parameters = CurrentParameters();
        }

        /// <summary>Wissel van vermogensbron, bijvoorbeeld van simulator naar Bluetooth.</summary>
        public void UseTrainer(ITrainer trainer)
        {
            float erg = Trainer?.ErgWatts ?? 0f; // een lopende workout gaat door op de nieuwe trainer
            Trainer?.SetErgWatts(0f);
            Trainer = trainer;
            Trainer.SetGrade(_gradePercent);
            Trainer.SetErgWatts(erg);
        }

        /// <summary>Een nieuwe rit begint (na <see cref="StartAt"/>); de vorige is dan voorbij.</summary>
        public event System.Action RideStarting;

        /// <summary>Begin de rit opnieuw op een afstand langs de route, stilstaand.</summary>
        public void StartAt(float distanceM)
        {
            RideStarting?.Invoke();
            DistanceM = distanceM;
            SpeedMs = 0f;
            ElapsedSeconds = 0f;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (Trainer == Simulator) Simulator.Tick(dt);

            SpeedMs = _physics.Step(SpeedMs, Trainer.PowerWatts, _gradePercent, dt, Brake);
            DistanceM += SpeedMs * dt;
            ElapsedSeconds += dt;
        }

        RiderParameters CurrentParameters() => new RiderParameters
        {
            TotalMassKg = totalMassKg,
            RollingResistance = rollingResistance,
            DragAreaM2 = dragAreaM2,
            AirDensity = RiderParameters.Default.AirDensity,
        };
    }
}
