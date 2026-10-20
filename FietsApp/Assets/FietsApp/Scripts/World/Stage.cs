using System.Collections.Generic;

namespace FietsApp.World
{
    /// <summary>Wat er gebeurde bij het kiezen van een klim voor de etappe.</summary>
    public enum StageChoice { Added, Removed, Full, OtherRegion }

    /// <summary>
    /// Een etappe (story 4.7): 2 tot 4 klimmen uit één gebied, na elkaar gereden. Na elke klim daal je af naar het
    /// knooppunt in het dal en rijd je door naar de voet van de volgende; zie docs/decisions.md ("Etappe").
    /// </summary>
    public sealed class Stage
    {
        public const int MinClimbs = 2, MaxClimbs = 4;

        readonly List<string> _climbs = new List<string>();

        /// <summary>De klimmen (id's) in rijvolgorde.</summary>
        public IReadOnlyList<string> Climbs => _climbs;

        /// <summary>Het gebied van de etappe, of null zolang er nog geen klim gekozen is.</summary>
        public string Region { get; private set; }

        public int Count => _climbs.Count;

        public bool CanStart => _climbs.Count >= MinClimbs;

        /// <summary>
        /// Kiest een klim: een nieuwe komt achteraan, een gekozen klim gaat er weer uit. Een klim uit een ander
        /// gebied of een vijfde klim wordt geweigerd.
        /// </summary>
        public StageChoice Toggle(string climb, string region)
        {
            if (_climbs.Remove(climb))
            {
                if (_climbs.Count == 0) Region = null;
                return StageChoice.Removed;
            }
            if (Region != null && region != Region) return StageChoice.OtherRegion;
            if (_climbs.Count >= MaxClimbs) return StageChoice.Full;
            _climbs.Add(climb);
            Region = region;
            return StageChoice.Added;
        }

        public void Clear()
        {
            _climbs.Clear();
            Region = null;
        }
    }
}
