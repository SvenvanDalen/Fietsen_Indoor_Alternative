namespace FietsBridge.Core;

public enum FtmsResult : byte
{
    Success = 0x01,
    NotSupported = 0x02,
    InvalidParameter = 0x03,
    OperationFailed = 0x04,
    ControlNotPermitted = 0x05,
}

public readonly record struct FtmsResponse(byte RequestOpCode, FtmsResult Result);

/// <summary>
/// Berichten voor het Fitness Machine Control Point (0x2AD9) en de Fitness Machine Feature (0x2ACC).
/// De FTMS-specificatie is niet vrij te downloaden; indeling gecontroleerd tegen GoldenCheetah
/// (src/Train/Ftms.h) en Auuki (src/ble/ftms/control-point.js), die met echte trainers werken.
/// </summary>
public static class FtmsControl
{
    public const byte OpRequestControl = 0x00;
    public const byte OpSetTargetPower = 0x05;
    public const byte OpStartOrResume = 0x07;
    public const byte OpSetSimulation = 0x11;
    public const byte OpResponse = 0x80;

    const int PowerTargetBit = 3;
    const int SimulationTargetBit = 13;

    public static byte[] RequestControl() => [OpRequestControl];

    public static byte[] StartOrResume() => [OpStartOrResume];

    /// <summary>ERG-modus (story 8.2): de trainer houdt zelf dit vermogen vast, ongeacht cadans (sint16, 1 W).</summary>
    public static byte[] SetTargetPower(int watts)
    {
        short w = (short)Math.Clamp(watts, 0, 4000);
        return [OpSetTargetPower, (byte)w, (byte)(w >> 8)];
    }

    /// <summary>
    /// Indoor bike simulation: windsnelheid (sint16, 0,001 m/s), helling (sint16, 0,01 %),
    /// rolweerstand Crr (uint8, 0,0001) en windweerstand (uint8, 0,01 kg/m). De trainer rekent hiermee zelf de weerstand uit.
    /// </summary>
    public static byte[] SetSimulation(float gradePercent, float windSpeed = 0f, float crr = 0.004f, float windResistance = 0.51f)
    {
        short wind = (short)Math.Round(Math.Clamp(windSpeed, -32f, 32f) * 1000f);
        short grade = (short)Math.Round(Math.Clamp(gradePercent, -40f, 40f) * 100f);
        byte c = (byte)Math.Round(Math.Clamp(crr, 0f, 0.0254f) * 10000f);
        byte w = (byte)Math.Round(Math.Clamp(windResistance, 0f, 2.54f) * 100f);
        return [OpSetSimulation, (byte)wind, (byte)(wind >> 8), (byte)grade, (byte)(grade >> 8), c, w];
    }

    /// <summary>Antwoord van de trainer op een opdracht: [0x80, opcode van het verzoek, resultaat].</summary>
    public static FtmsResponse? ParseResponse(ReadOnlySpan<byte> data) =>
        data.Length >= 3 && data[0] == OpResponse ? new FtmsResponse(data[1], (FtmsResult)data[2]) : null;

    /// <summary>Uit de Feature-characteristic: kan de trainer een helling nabootsen?</summary>
    public static bool SupportsSimulation(ReadOnlySpan<byte> features) => HasTarget(features, SimulationTargetBit);

    /// <summary>Uit de Feature-characteristic: kan de trainer een vast vermogen vasthouden (ERG)?</summary>
    public static bool SupportsPower(ReadOnlySpan<byte> features) => HasTarget(features, PowerTargetBit);

    /// <summary>4 bytes machine-features, dan 4 bytes target-settings met per bit een instelbare doelwaarde.</summary>
    static bool HasTarget(ReadOnlySpan<byte> features, int bit)
    {
        if (features.Length < 8) return false;
        uint targets = (uint)(features[4] | features[5] << 8 | features[6] << 16 | features[7] << 24);
        return (targets & (1u << bit)) != 0;
    }
}

/// <summary>
/// Bepaalt wanneer een nieuwe helling naar de trainer gaat (story 1.2): hoogstens elke
/// <see cref="MinInterval"/>, alleen bij een merkbaar verschil, en grote sprongen in kleine stappen,
/// zodat de weerstand niet schokt (backlog sectie 5, risico "schokkerige weerstand").
/// </summary>
public sealed class GradeSender
{
    public static readonly TimeSpan MinInterval = TimeSpan.FromMilliseconds(500);
    public const float MinChange = 0.1f;        // % — kleiner voel je niet
    public const float MaxRatePerSecond = 4f;   // % per seconde

    float? _lastSent;
    DateTimeOffset _lastTime;

    /// <summary>De helling om nu te versturen, of null als er (nog) niets verstuurd hoeft te worden.</summary>
    public float? Next(float desired, DateTimeOffset now)
    {
        if (_lastSent is not { } last) return Send(desired, now);

        double elapsed = (now - _lastTime).TotalSeconds;
        if (elapsed < MinInterval.TotalSeconds || Math.Abs(desired - last) < MinChange) return null;

        float maxStep = (float)(MaxRatePerSecond * elapsed);
        return Send(last + Math.Clamp(desired - last, -maxStep, maxStep), now);
    }

    /// <summary>Opnieuw beginnen, bijvoorbeeld nadat de trainer opnieuw verbonden is.</summary>
    public void Reset() => _lastSent = null;

    float Send(float grade, DateTimeOffset now)
    {
        grade = (float)Math.Round(grade, 2);
        _lastSent = grade;
        _lastTime = now;
        return grade;
    }
}

/// <summary>
/// Bepaalt wanneer een nieuw ERG-vermogen naar de trainer gaat (story 8.2): een nieuw doel meteen, maar
/// hoogstens elke <see cref="MinInterval"/>; hetzelfde doel niet opnieuw. Geen geleidelijke overgang: een
/// workout wil een stap, en de trainer regelt zelf hoe snel hij die haalt.
/// </summary>
public sealed class PowerSender
{
    public static readonly TimeSpan MinInterval = TimeSpan.FromMilliseconds(500);

    int? _lastSent;
    DateTimeOffset _lastTime;

    /// <summary>Het vermogen om nu te versturen, of null als er niets verstuurd hoeft te worden.</summary>
    public int? Next(int desired, DateTimeOffset now)
    {
        if (_lastSent == desired) return null;
        if (_lastSent is not null && now - _lastTime < MinInterval) return null;
        _lastSent = desired;
        _lastTime = now;
        return desired;
    }

    /// <summary>Opnieuw beginnen, bijvoorbeeld na een mislukte opdracht of na de helling.</summary>
    public void Reset() => _lastSent = null;
}
