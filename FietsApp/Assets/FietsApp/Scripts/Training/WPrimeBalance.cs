using System;

namespace FietsApp.Training
{
    /// <summary>
    /// W′-balans (story 8.7): hoeveel anaerobe reserve er nog is. Boven de kritisch vermogen (CP, hier de FTP) gaat de
    /// reserve op, eronder herstelt ze exponentieel naar vol. Herstelsnelheid volgens Skiba (2015):
    /// tau = 546·e^(−0,01·DCP) + 316 seconden, met DCP het verschil tussen CP en het vermogen.
    /// </summary>
    public sealed class WPrimeBalance
    {
        public const double DefaultWPrimeJoules = 20000;

        readonly double _cp;

        public WPrimeBalance(double criticalPowerWatts, double wPrimeJoules = DefaultWPrimeJoules)
        {
            _cp = criticalPowerWatts;
            WPrime = wPrimeJoules > 0 ? wPrimeJoules : DefaultWPrimeJoules;
            Balance = WPrime;
            Lowest = WPrime;
        }

        public double WPrime { get; }

        /// <summary>Wat er nog over is, in joule (0 = leeg).</summary>
        public double Balance { get; private set; }

        /// <summary>De laagste stand tot nu toe, voor de ritsamenvatting.</summary>
        public double Lowest { get; private set; }

        /// <summary>Hoeveel er nog over is, van 0 (leeg) tot 1 (vol).</summary>
        public double Fraction => Math.Clamp(Balance / WPrime, 0, 1);

        public double LowestFraction => Math.Clamp(Lowest / WPrime, 0, 1);

        /// <summary>Verwerk <paramref name="dt"/> seconden aan <paramref name="watts"/>.</summary>
        public void Add(double watts, double dt)
        {
            if (dt <= 0) return;
            if (watts > _cp)
                Balance -= (watts - _cp) * dt;
            else
            {
                double tau = 546.0 * Math.Exp(-0.01 * (_cp - watts)) + 316.0;
                Balance += (WPrime - Balance) * (1.0 - Math.Exp(-dt / tau));
            }
            if (Balance > WPrime) Balance = WPrime;
            // Onder nul kan het in het model wel; voor de balk houden we 0 aan als bodem, de reserve is dan op.
            if (Balance < 0) Balance = 0;
            if (Balance < Lowest) Lowest = Balance;
        }
    }
}
