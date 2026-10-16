using System.Collections.Generic;

namespace FietsApp.Training
{
    /// <summary>
    /// Tijdgewogen gemiddelde over de laatste <see cref="WindowSeconds"/> seconden, bijvoorbeeld 3-seconden-vermogen
    /// voor de zone (story 8.5): een losse trap boven of onder de grens laat de kleur dan niet knipperen.
    /// </summary>
    public sealed class RollingAverage
    {
        readonly LinkedList<(double value, double dt)> _samples = new LinkedList<(double, double)>();
        double _sum, _time;

        public RollingAverage(double windowSeconds) => WindowSeconds = windowSeconds;

        public double WindowSeconds { get; }
        public double Value => _time > 0 ? _sum / _time : 0;

        /// <summary>Voeg een meting toe die <paramref name="dt"/> seconden gold.</summary>
        public void Add(double value, double dt)
        {
            if (dt <= 0) return;
            _samples.AddLast((value, dt));
            _sum += value * dt;
            _time += dt;
            while (_time > WindowSeconds && _samples.Count > 1)
            {
                var (oldValue, oldDt) = _samples.First.Value;
                double excess = _time - WindowSeconds;
                double removed = oldDt <= excess ? oldDt : excess; // de oudste meting valt (deels) uit het venster
                if (removed == oldDt) _samples.RemoveFirst();
                else _samples.First.Value = (oldValue, oldDt - removed);
                _sum -= oldValue * removed;
                _time -= removed;
            }
        }

        public void Clear()
        {
            _samples.Clear();
            _sum = _time = 0;
        }
    }
}
