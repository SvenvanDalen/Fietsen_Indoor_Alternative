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
        [Tooltip("Rijder plus fiets; met 'Gewicht uit profiel' aan komt hij uit het profiel (story 7.2).")]
        [SerializeField, Min(30f)] float totalMassKg = 80f;
        [SerializeField] bool massFromProfile = true;
        [SerializeField, Range(0.002f, 0.02f)] float rollingResistance = 0.004f;
        [SerializeField, Range(0.15f, 0.6f)] float dragAreaM2 = 0.32f;

        RiderPhysics _physics;
        FietsApp.World.Surface _surface;
        float _gradePercent;
        float _gradeScale = 1f;

        public ITrainer Trainer { get; private set; }
        public SimulatorTrainer Simulator { get; private set; }

        public float SpeedMs { get; private set; }
        public float DistanceM { get; private set; }
        public float ElapsedSeconds { get; private set; }

        /// <summary>Remmen, van 0 (los) tot 1 (vol); bijvoorbeeld de spatiebalk. Werkt met elke trainer.</summary>
        public float Brake { get; set; }

        /// <summary>
        /// Hoogste veilige snelheid hier (m/s), zoals voor een bocht in de afdaling (story 4.12); de rijder remt vanzelf
        /// bij als hij erboven komt. Oneindig = geen grens.
        /// </summary>
        public float SpeedLimit { get; set; } = float.PositiveInfinity;

        /// <summary>
        /// Ondergrond onder de wielen (story 4.13): de fysica rekent met zijn rolweerstand, en de trainer voelt het
        /// verschil met asfalt als extra helling.
        /// </summary>
        public FietsApp.World.Surface Surface
        {
            get => _surface;
            set
            {
                if (_surface == value) return;
                _surface = value;
                if (_physics != null) _physics.Parameters = CurrentParameters();
                PushGrade();
            }
        }

        /// <summary>Of de rijder deze frame vanzelf bijremde voor een bocht.</summary>
        public bool CornerBraking { get; private set; }

        /// <summary>
        /// Helling van de route; de fysica rekent hiermee. De trainer krijgt hem maal <see cref="GradeScale"/>,
        /// begrensd op zijn maximum.
        /// </summary>
        public float GradePercent
        {
            get => _gradePercent;
            set
            {
                _gradePercent = value;
                PushGrade();
            }
        }

        /// <summary>
        /// Weerstandsgevoeligheid (story 2.3): welk deel van de helling de trainer laat voelen, 0,5-1,5. Snelheid
        /// en klimtijd blijven op de echte helling, zodat tijden vergelijkbaar blijven.
        /// </summary>
        public float GradeScale
        {
            get => _gradeScale;
            set
            {
                _gradeScale = Mathf.Clamp(value, 0.5f, 1.5f);
                PushGrade();
            }
        }

        void Awake()
        {
            if (massFromProfile)
            {
                totalMassKg = ProfileStore.Current.TotalMassKg;
                ProfileStore.Changed += OnProfileChanged;
            }
            _physics = new RiderPhysics(CurrentParameters());
            Simulator = new SimulatorTrainer();
            UseTrainer(Simulator);
        }

        /// <summary>
        /// Zet de rit stil namens <paramref name="owner"/> (pauze, ritmenu, wereldkaart; story 6.4). De rit loopt pas
        /// weer als iedereen die hem stilzette hem ook weer vrijgeeft, zodat het sluiten van een menu een pauze niet opheft.
        /// Stil telt er geen tijd of afstand, en de trainer krijgt een vlakke weg zodat je makkelijk weer wegrijdt.
        /// </summary>
        public void Hold(object owner)
        {
            _holds.Add(owner);
            enabled = false;
        }

        /// <summary>Geeft de rit vrij namens <paramref name="owner"/>; zie <see cref="Hold"/>.</summary>
        public void Release(object owner)
        {
            _holds.Remove(owner);
            enabled = _holds.Count == 0;
        }

        /// <summary>Of <paramref name="owner"/> de rit stilhoudt.</summary>
        public bool IsHeldBy(object owner) => _holds.Contains(owner);

        readonly System.Collections.Generic.HashSet<object> _holds = new System.Collections.Generic.HashSet<object>();

        void OnEnable() => PushGrade();

        void OnDisable() => PushGrade();

        /// <summary>Helling naar de trainer: die van de route maal de gevoeligheid, of vlak als de rit stilstaat.</summary>
        void PushGrade() => Trainer?.SetGrade(isActiveAndEnabled
            ? _gradePercent * _gradeScale + FietsApp.World.Surfaces.TrainerGradeBonus(_surface)
            : 0f);

        void OnDestroy() => ProfileStore.Changed -= OnProfileChanged;

        /// <summary>Gewicht of fiets gewijzigd (of een profiel van een ander apparaat binnengehaald): andere massa voor de fysica.</summary>
        void OnProfileChanged()
        {
            totalMassKg = ProfileStore.Current.TotalMassKg;
            if (_physics != null) _physics.Parameters = CurrentParameters();
        }

        /// <summary>Rijder plus fiets in kg, zoals de fysica rekent.</summary>
        public float TotalMassKg => totalMassKg;

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
            PushGrade();
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

            float free = _physics.Step(SpeedMs, Trainer.PowerWatts, _gradePercent, dt, Brake);
            SpeedMs = _physics.Step(SpeedMs, Trainer.PowerWatts, _gradePercent, dt, Brake, SpeedLimit);
            CornerBraking = SpeedMs < free - 1e-4f;
            DistanceM += SpeedMs * dt;
            ElapsedSeconds += dt;
        }

        RiderParameters CurrentParameters() => new RiderParameters
        {
            TotalMassKg = totalMassKg,
            // Op asfalt de ingestelde waarde; op andere ondergrond de hogere Crr daarvan, met hetzelfde verschil erbovenop.
            RollingResistance = rollingResistance + FietsApp.World.Surfaces.RollingResistance(_surface)
                                - FietsApp.World.Surfaces.RollingResistance(FietsApp.World.Surface.Asphalt),
            // De gekozen fiets (7.3) maakt de rijder iets meer of minder aerodynamisch.
            DragAreaM2 = dragAreaM2 + (massFromProfile ? ProfileStore.Current.Bike.DragAreaDeltaM2 : 0f),
            AirDensity = RiderParameters.Default.AirDensity,
        };
    }
}
