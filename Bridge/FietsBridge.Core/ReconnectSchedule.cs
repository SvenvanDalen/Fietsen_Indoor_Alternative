namespace FietsBridge.Core;

/// <summary>
/// Wanneer de brug een weggevallen apparaat opnieuw probeert (story 1.5): eerst na een halve seconde (vaak is het
/// maar een hapering), daarna elke 2 seconden, en na een minuut geeft hij het op.
/// </summary>
public static class ReconnectSchedule
{
    public static readonly TimeSpan First = TimeSpan.FromSeconds(0.5);
    public static readonly TimeSpan Interval = TimeSpan.FromSeconds(2);
    public static readonly TimeSpan GiveUpAfter = TimeSpan.FromSeconds(60);

    /// <summary>Wachttijd vóór poging <paramref name="attempt"/> (vanaf 0), of null als het na <paramref name="elapsed"/> sinds het wegvallen genoeg is geweest.</summary>
    public static TimeSpan? Delay(int attempt, TimeSpan elapsed) =>
        elapsed >= GiveUpAfter ? null : attempt == 0 ? First : Interval;
}
