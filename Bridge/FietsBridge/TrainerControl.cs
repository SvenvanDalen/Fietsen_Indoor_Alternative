using FietsBridge.Core;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
using Windows.Storage.Streams;

namespace FietsBridge;

/// <summary>
/// Laat een FTMS-trainer de helling nabootsen (story 1.2) of een vast vermogen vasthouden (ERG, story 8.2).
/// Vraagt bij de eerste opdracht besturing aan (0x00) en start de trainer (0x07); daarna gaat elke 250 ms
/// het ERG-vermogen (0x05, via <see cref="PowerSender"/>) of anders de helling (0x11, via
/// <see cref="GradeSender"/>, geleidelijk en zonder schokken) naar het Control Point. Zolang er een
/// ERG-vermogen is, wordt de helling genegeerd.
/// </summary>
sealed class TrainerControl : IDisposable
{
    static readonly Guid FeatureUuid = GattServices.FromShort(0x2ACC);
    static readonly Guid ControlPointUuid = GattServices.FromShort(0x2AD9);
    static readonly TimeSpan ResponseTimeout = TimeSpan.FromSeconds(2);

    readonly GattCharacteristic _controlPoint;
    readonly GradeSender _sender = new();
    readonly PowerSender _powerSender = new();
    readonly CancellationTokenSource _cts = new();
    TaskCompletionSource<FtmsResponse>? _pending;
    volatile bool _hasControl;
    float? _desired;
    volatile int _desiredPower; // 0 = geen ERG
    bool _ergActive;

    public bool SupportsSimulation { get; }
    public bool SupportsPower { get; }

    /// <summary>Uitleg voor de gebruiker, bijv. "helling actief" of waarom het niet lukt.</summary>
    public event Action<string>? StatusChanged;

    TrainerControl(GattCharacteristic controlPoint, bool supportsSimulation, bool supportsPower)
    {
        _controlPoint = controlPoint;
        SupportsSimulation = supportsSimulation;
        SupportsPower = supportsPower;
    }

    /// <summary>Null als de trainer geen Control Point heeft (dan kan hij alleen meten).</summary>
    public static async Task<TrainerControl?> CreateAsync(GattDeviceService ftms)
    {
        var points = await ftms.GetCharacteristicsForUuidAsync(ControlPointUuid, BluetoothCacheMode.Uncached);
        var controlPoint = points.Status == GattCommunicationStatus.Success ? points.Characteristics.FirstOrDefault() : null;
        if (controlPoint is null) return null;

        bool simulation = false, power = false;
        var features = await ftms.GetCharacteristicsForUuidAsync(FeatureUuid, BluetoothCacheMode.Uncached);
        var feature = features.Status == GattCommunicationStatus.Success ? features.Characteristics.FirstOrDefault() : null;
        if (feature is not null)
        {
            var read = await feature.ReadValueAsync(BluetoothCacheMode.Uncached);
            if (read.Status == GattCommunicationStatus.Success)
            {
                var bytes = ToBytes(read.Value);
                simulation = FtmsControl.SupportsSimulation(bytes);
                power = FtmsControl.SupportsPower(bytes);
            }
        }

        var control = new TrainerControl(controlPoint, simulation, power);
        controlPoint.ValueChanged += (_, args) =>
        {
            if (FtmsControl.ParseResponse(ToBytes(args.CharacteristicValue)) is { } response)
                control._pending?.TrySetResult(response);
        };
        // Antwoorden komen als indication; zonder abonnement krijgen we nooit "gelukt" terug.
        await controlPoint.WriteClientCharacteristicConfigurationDescriptorAsync(
            GattClientCharacteristicConfigurationDescriptorValue.Indicate);

        _ = control.LoopAsync(control._cts.Token);
        return control;
    }

    public void SetGrade(float gradePercent)
    {
        if (!SupportsSimulation && _desired is null)
            StatusChanged?.Invoke("trainer kan geen helling nabootsen (geen FTMS-simulatie)");
        _desired = gradePercent;
    }

    /// <summary>ERG-vermogen in watt; null of 0 zet de trainer terug op de helling.</summary>
    public void SetTargetPower(int? watts)
    {
        int target = watts is > 0 ? watts.Value : 0;
        if (target > 0 && !SupportsPower && _desiredPower == 0)
            StatusChanged?.Invoke("trainer kan geen vast vermogen vasthouden (geen ERG)");
        _desiredPower = target;
    }

    async Task LoopAsync(CancellationToken ct)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(250));
        try
        {
            while (await timer.WaitForNextTickAsync(ct))
            {
                int watts = _desiredPower;
                if (SupportsPower && watts > 0)
                {
                    if (_powerSender.Next(watts, DateTimeOffset.Now) is { } send) await SendPowerAsync(send);
                    continue;
                }
                if (_ergActive)
                {
                    // Terug van ERG naar de helling: de huidige helling meteen opnieuw sturen.
                    _ergActive = false;
                    _powerSender.Reset();
                    _sender.Reset();
                    StatusChanged?.Invoke("ERG uit, helling actief");
                }
                if (!SupportsSimulation || _desired is not { } desired) continue;
                if (_sender.Next(desired, DateTimeOffset.Now) is { } grade) await SendGradeAsync(grade);
            }
        }
        catch (OperationCanceledException) { }
    }

    /// <summary>Besturing aanvragen en de trainer starten, eenmalig; false als de trainer weigert.</summary>
    async Task<bool> EnsureControlAsync(string active)
    {
        if (_hasControl) return true;
        var control = await SendAsync(FtmsControl.RequestControl());
        if (control != FtmsResult.Success)
        {
            StatusChanged?.Invoke($"trainer geeft geen besturing ({Describe(control)})");
            return false;
        }
        await SendAsync(FtmsControl.StartOrResume()); // sommige trainers vereisen dit; resultaat maakt niet uit
        _hasControl = true;
        StatusChanged?.Invoke(active);
        return true;
    }

    async Task SendPowerAsync(int watts)
    {
        if (!await EnsureControlAsync("ERG actief"))
        {
            _powerSender.Reset(); // volgende ronde opnieuw proberen
            return;
        }
        var result = await SendAsync(FtmsControl.SetTargetPower(watts));
        if (result == FtmsResult.Success)
        {
            if (!_ergActive) StatusChanged?.Invoke($"ERG {watts} W");
            _ergActive = true;
            return;
        }
        if (result == FtmsResult.ControlNotPermitted) _hasControl = false; // bijv. andere app nam het over
        StatusChanged?.Invoke($"vermogen {watts} W niet gezet ({Describe(result)})");
        _powerSender.Reset();
    }

    async Task SendGradeAsync(float grade)
    {
        if (!await EnsureControlAsync("helling actief"))
        {
            _sender.Reset(); // volgende ronde opnieuw proberen
            return;
        }

        var result = await SendAsync(FtmsControl.SetSimulation(grade));
        if (result == FtmsResult.Success) return;

        if (result == FtmsResult.ControlNotPermitted) _hasControl = false; // bijv. andere app nam het over
        StatusChanged?.Invoke($"helling {grade:0.0}% niet gezet ({Describe(result)})");
        _sender.Reset();
    }

    /// <summary>Schrijft een opdracht en wacht op het antwoord van de trainer; null bij geen antwoord.</summary>
    async Task<FtmsResult?> SendAsync(byte[] message)
    {
        var pending = new TaskCompletionSource<FtmsResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
        _pending = pending;
        try
        {
            var status = await _controlPoint.WriteValueWithResultAsync(ToBuffer(message), GattWriteOption.WriteWithResponse);
            if (status.Status != GattCommunicationStatus.Success) return null;
        }
        catch (Exception e) when (e is System.Runtime.InteropServices.COMException or ObjectDisposedException)
        {
            return null;
        }

        var done = await Task.WhenAny(pending.Task, Task.Delay(ResponseTimeout));
        return done == pending.Task && pending.Task.Result.RequestOpCode == message[0] ? pending.Task.Result.Result : null;
    }

    static string Describe(FtmsResult? result) => result switch
    {
        null => "geen antwoord",
        FtmsResult.NotSupported => "niet ondersteund",
        FtmsResult.InvalidParameter => "ongeldige waarde",
        FtmsResult.OperationFailed => "mislukt",
        FtmsResult.ControlNotPermitted => "besturing niet toegestaan; heeft een andere app de trainer?",
        _ => result.ToString()!,
    };

    static byte[] ToBytes(IBuffer buffer)
    {
        var bytes = new byte[buffer.Length];
        using var reader = DataReader.FromBuffer(buffer);
        reader.ReadBytes(bytes);
        return bytes;
    }

    static IBuffer ToBuffer(byte[] bytes)
    {
        using var writer = new DataWriter();
        writer.WriteBytes(bytes);
        return writer.DetachBuffer();
    }

    public void Dispose() => _cts.Cancel();
}
