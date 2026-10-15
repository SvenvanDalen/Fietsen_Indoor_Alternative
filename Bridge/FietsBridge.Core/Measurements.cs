using System.Buffers.Binary;

namespace FietsBridge.Core;

/// <summary>Eén meting van een apparaat; null = het apparaat stuurde deze waarde niet mee.</summary>
public readonly record struct Measurement(int? PowerWatts = null, float? CadenceRpm = null, int? HeartRateBpm = null);

public readonly record struct CrankData(ushort CumulativeRevolutions, ushort LastEventTime1024);

public readonly record struct CyclingPowerData(int PowerWatts, CrankData? Crank);

/// <summary>
/// FTMS Indoor Bike Data (0x2AD2). Velden staan achter elkaar in de volgorde van de flag-bits;
/// alleen aanwezige velden nemen ruimte in.
/// </summary>
public static class IndoorBikeData
{
    // Veldgroottes per flag-bit (bit 0 heeft omgekeerde betekenis: 0 = snelheid aanwezig).
    // Bit 5 (Resistance Level) is 2 bytes in FTMS 1.0 en 1 byte in de huidige GSS; zie Parse.
    static readonly int[] FieldSizes = [2, 2, 2, 2, 3, 2, 2, 2, 5, 1, 1, 2, 2];
    const int CadenceBit = 2, ResistanceBit = 5, PowerBit = 6, HeartRateBit = 9;

    public static Measurement Parse(ReadOnlySpan<byte> data)
    {
        if (data.Length < 2) return new Measurement();
        int flags = BinaryPrimitives.ReadUInt16LittleEndian(data);

        bool Present(int bit) => bit == 0 ? (flags & 1) == 0 : (flags & (1 << bit)) != 0;

        int resistanceSize = 2;
        if (Present(ResistanceBit))
        {
            int expected = 2 + Enumerable.Range(0, FieldSizes.Length).Where(Present).Sum(b => FieldSizes[b]);
            if (data.Length == expected - 1) resistanceSize = 1;
        }

        int? power = null, heartRate = null;
        float? cadence = null;
        int offset = 2;
        for (int bit = 0; bit < FieldSizes.Length; bit++)
        {
            if (!Present(bit)) continue;
            int size = bit == ResistanceBit ? resistanceSize : FieldSizes[bit];
            if (offset + size > data.Length) return new Measurement(); // afgekapt bericht

            var field = data.Slice(offset, size);
            switch (bit)
            {
                case CadenceBit: cadence = BinaryPrimitives.ReadUInt16LittleEndian(field) * 0.5f; break;
                case PowerBit: power = BinaryPrimitives.ReadInt16LittleEndian(field); break;
                case HeartRateBit: heartRate = field[0]; break;
            }
            offset += size;
        }
        return new Measurement(power, cadence, heartRate);
    }
}

/// <summary>Cycling Power Measurement (0x2A63): vermogen en eventueel crankomwentelingen voor de cadans.</summary>
public static class CyclingPowerMeasurement
{
    const int BalanceBit = 0, TorqueBit = 2, WheelBit = 4, CrankBit = 5;

    public static CyclingPowerData Parse(ReadOnlySpan<byte> data)
    {
        if (data.Length < 4) return new CyclingPowerData(0, null);
        int flags = BinaryPrimitives.ReadUInt16LittleEndian(data);
        int power = BinaryPrimitives.ReadInt16LittleEndian(data[2..]);

        int offset = 4;
        if ((flags & (1 << BalanceBit)) != 0) offset += 1;
        if ((flags & (1 << TorqueBit)) != 0) offset += 2;
        if ((flags & (1 << WheelBit)) != 0) offset += 6;

        CrankData? crank = null;
        if ((flags & (1 << CrankBit)) != 0 && offset + 4 <= data.Length)
        {
            crank = new CrankData(
                BinaryPrimitives.ReadUInt16LittleEndian(data[offset..]),
                BinaryPrimitives.ReadUInt16LittleEndian(data[(offset + 2)..]));
        }
        return new CyclingPowerData(power, crank);
    }
}

/// <summary>
/// Rekent cadans uit opeenvolgende crankgegevens. Tellers lopen rond bij 65536.
/// Komen er een tijd geen nieuwe omwentelingen bij, dan staat de rijder stil: cadans 0.
/// </summary>
public sealed class CrankCadence(TimeSpan? stopAfter = null)
{
    readonly TimeSpan _stopAfter = stopAfter ?? TimeSpan.FromSeconds(3);
    CrankData? _last;
    DateTimeOffset _lastChange;
    float? _cadence;

    public float? Update(CrankData crank, DateTimeOffset now)
    {
        if (_last is { } last && crank.LastEventTime1024 != last.LastEventTime1024)
        {
            int revs = (ushort)(crank.CumulativeRevolutions - last.CumulativeRevolutions);
            int ticks = (ushort)(crank.LastEventTime1024 - last.LastEventTime1024);
            _cadence = revs * 60f * 1024f / ticks;
            _lastChange = now;
        }
        else if (_last is null)
        {
            _lastChange = now;
        }
        else if (now - _lastChange > _stopAfter)
        {
            _cadence = 0f;
        }

        _last = crank;
        return _cadence;
    }
}

/// <summary>Heart Rate Measurement (0x2A37).</summary>
public static class HeartRateMeasurement
{
    public static int? Parse(ReadOnlySpan<byte> data)
    {
        if (data.Length < 2) return null;
        bool sixteenBit = (data[0] & 1) != 0;
        if (!sixteenBit) return data[1];
        return data.Length >= 3 ? BinaryPrimitives.ReadUInt16LittleEndian(data[1..]) : null;
    }
}
