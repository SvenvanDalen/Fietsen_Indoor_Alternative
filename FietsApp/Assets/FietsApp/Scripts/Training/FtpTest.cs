using System;

namespace FietsApp.Training
{
    /// <summary>
    /// Een FTP-test (story 8.1). Krijgt elk frame de gemeten waarden, zegt welk ERG-vermogen de trainer moet
    /// vasthouden (0 = vrij rijden) en geeft aan het eind een FTP.
    /// </summary>
    public interface IFtpTest
    {
        string Name { get; }
        string PhaseName { get; }

        /// <summary>Uitleg voor nu, bijvoorbeeld wat de rijder moet doen.</summary>
        string Instruction { get; }

        /// <summary>Seconden tot het volgende moment (volgende stap of einde van de fase); 0 als dat er niet is.</summary>
        double PhaseRemaining { get; }

        bool Finished { get; }

        /// <summary>De gemeten FTP in watt, zodra de test klaar is; anders null.</summary>
        int? ResultFtp { get; }

        /// <summary>ERG-doel nu in watt, of 0 om vrij te rijden.</summary>
        int ErgWatts { get; }

        void Advance(double dt, double powerWatts);

        /// <summary>De rijder stopt zelf (bij de ramptest het normale einde).</summary>
        void Stop();
    }

    /// <summary>
    /// Ramptest: ERG begint op <see cref="StartWatts"/> en gaat elke minuut <see cref="StepWatts"/> omhoog tot de
    /// rijder het niet meer haalt. FTP = 75% van de beste minuut, de gangbare rekenregel voor een ramp van 1 minuut.
    /// De test stopt vanzelf als het vermogen na de eerste stap <see cref="FailSeconds"/> s onder de helft van het
    /// doel blijft.
    /// </summary>
    public sealed class RampTest : IFtpTest
    {
        public const int StartWatts = 100, StepWatts = 20;
        public const double StepSeconds = 60, FailSeconds = 5, FtpFactor = 0.75;

        readonly RollingAverage _minute = new RollingAverage(60);
        double _elapsed, _belowFor;

        public string Name => "Ramptest";
        public string PhaseName => Finished ? "Klaar" : $"Stap {Step + 1}";
        public string Instruction => Finished
            ? $"Beste minuut {BestMinuteWatts:0} W"
            : $"Houd {ErgWatts} W vast zo lang je kunt; X stopt. Beste minuut {BestMinuteWatts:0} W";
        public double PhaseRemaining => Finished ? 0 : StepSeconds - _elapsed % StepSeconds;
        public bool Finished { get; private set; }
        public double BestMinuteWatts { get; private set; }
        public int? ResultFtp => Finished && BestMinuteWatts > 0 ? (int?)Math.Round(FtpFactor * BestMinuteWatts) : null;
        public int ErgWatts => Finished ? 0 : StartWatts + StepWatts * Step;
        public double Elapsed => _elapsed;

        int Step => (int)(_elapsed / StepSeconds);

        public void Advance(double dt, double powerWatts)
        {
            if (Finished || dt <= 0) return;
            int target = ErgWatts;
            _elapsed += dt;
            _minute.Add(powerWatts, dt);
            if (_minute.Duration >= 60 - 1e-9) BestMinuteWatts = Math.Max(BestMinuteWatts, _minute.Value);

            // Niet meer bij te houden: een tijdje onder de helft van het doel (niet in de eerste stap, dan kom je op gang).
            _belowFor = _elapsed > StepSeconds && powerWatts < target * 0.5 ? _belowFor + dt : 0;
            if (_belowFor >= FailSeconds) Finished = true;
        }

        public void Stop() => Finished = true;
    }

    /// <summary>
    /// 20-minutentest: 10 min opwarmen in ERG (oplopend naar 75% van de huidige FTP), 20 min vrij zo hard als je
    /// volhoudt, dan 5 min uitrijden. FTP = 95% van het gemiddelde over de 20 minuten.
    /// </summary>
    public sealed class TwentyMinuteTest : IFtpTest
    {
        public const double WarmupSeconds = 600, TestSeconds = 1200, CooldownSeconds = 300, FtpFactor = 0.95;

        readonly int _currentFtp;
        double _elapsed, _testWork, _testTime;
        bool _stopped;

        public TwentyMinuteTest(int currentFtp) => _currentFtp = currentFtp;

        public string Name => "20-minutentest";
        public string PhaseName => Finished ? "Klaar" : _elapsed < WarmupSeconds ? "Opwarmen"
            : _elapsed < WarmupSeconds + TestSeconds ? "Test" : "Uitrijden";
        public string Instruction => PhaseName switch
        {
            "Opwarmen" => "Rustig opwarmen; de trainer regelt het vermogen",
            "Test" => $"Rijd zo hard als je 20 minuten volhoudt. Gemiddeld {AverageWatts:0} W",
            "Uitrijden" => $"Uitrijden. Gemiddeld {AverageWatts:0} W, FTP {ResultIfDone:0} W",
            _ => $"Gemiddeld {AverageWatts:0} W over de test",
        };
        public double PhaseRemaining => PhaseName switch
        {
            "Opwarmen" => WarmupSeconds - _elapsed,
            "Test" => WarmupSeconds + TestSeconds - _elapsed,
            "Uitrijden" => WarmupSeconds + TestSeconds + CooldownSeconds - _elapsed,
            _ => 0,
        };

        /// <summary>Klaar na het uitrijden, of eerder als de rijder stopt nadat de 20 minuten erop zitten.</summary>
        public bool Finished => _stopped || _elapsed >= WarmupSeconds + TestSeconds + CooldownSeconds;
        public double AverageWatts => _testTime > 0 ? _testWork / _testTime : 0;
        double ResultIfDone => FtpFactor * AverageWatts;

        /// <summary>Alleen een uitslag als de volle 20 minuten gereden zijn.</summary>
        public int? ResultFtp => Finished && _testTime >= TestSeconds - 1e-6 ? (int?)Math.Round(ResultIfDone) : null;

        public int ErgWatts => PhaseName switch
        {
            "Opwarmen" => (int)Math.Round(_currentFtp * (0.45 + 0.30 * _elapsed / WarmupSeconds)),
            "Uitrijden" => (int)Math.Round(_currentFtp * 0.50),
            _ => 0,
        };

        public void Advance(double dt, double powerWatts)
        {
            if (Finished || dt <= 0) return;
            double testStart = WarmupSeconds, testEnd = WarmupSeconds + TestSeconds;
            // Het deel van deze stap dat binnen de testfase valt.
            double inTest = Math.Max(0, Math.Min(_elapsed + dt, testEnd) - Math.Max(_elapsed, testStart));
            _testWork += powerWatts * inTest;
            _testTime += inTest;
            _elapsed += dt;
        }

        public void Stop() => _stopped = true;
    }
}
