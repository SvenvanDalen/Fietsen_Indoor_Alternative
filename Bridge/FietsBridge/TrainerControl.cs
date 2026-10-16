using FietsBridge.Core;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
using Windows.Storage.Streams;

namespace FietsBridge;

/// <summary>
/// Laat een FTMS-trainer de helling nabootsen (story 1.2). Vraagt bij de eerste helling besturing aan
/// (0x00) en start de trainer (0x07); daarna gaat elke 250 ms de gewenste helling via
/// <see cref="GradeSender"/> naar het Control Point (0x11), geleidelijk en zonder schokken.
/// </summary>
sealed class TrainerControl : IDisposable
{
    static readonly Guid FeatureUuid = GattServices.FromShort(0x2ACC);
    static readonly Guid ControlPointUuid = GattServices.FromShort(0x2AD9);
    static readonly TimeSpan ResponseTimeout = TimeSpan.FromSeconds(2);

    readonly GattCharacteristic _controlPoint;
    readonly GradeSender _sender = new();
    readonly CancellationTokenSource _cts = new();
    TaskCompletionSource<FtmsResponse>? _pending;
    volatile bool _hasControl;
    float? _desired;

    public bool SupportsSimulation { get; }

    /// <summary>Uitleg voor de gebruiker, bijv. "helling actief" of waarom het niet lukt.</summary>
    public event Action<string>? StatusChanged;

    TrainerControl(GattCharacteristic controlPoint, bool supportsSimulation)
    {
        _controlPoint = controlPoint;
        SupportsSimulation = supportsSimulation;
    }

    /// <summary>Null als de trainer geen Control Point heeft (dan kan hij alleen meten).</summary>
    public static async Task<TrainerControl?> CreateAsync(GattDeviceService ftms)
    {
        var points = await ftms.GetCharacteristicsForUuidAsync(ControlPointUuid, BluetoothCacheMode.Uncached);
        var controlPoint = points.Status == GattCommunicationStatus.Success ? points.Characteristics.FirstOrDefault() : null;
        if (controlPoint is null) return null;

        bool simulation = false;
        var features = await ftms.GetCharacteristicsForUuidAsync(FeatureUuid, BluetoothCacheMode.Uncached);
        var feature = features.Status == GattCommunicationStatus.Success ? features.Characteristics.FirstOrDefault() : null;
        if (feature is not null)
        {
            var read = await feature.ReadValueAsync(BluetoothCacheMode.Uncached);
            if (read.Status == GattCommunicationStatus.Success) simulation = FtmsControl.SupportsSimulation(ToBytes(read.Value));
        }

        var control = new TrainerControl(controlPoint, simulation);
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

    async Task LoopAsync(CancellationToken ct)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(250));
        try
        {
            while (await timer.WaitForNextTickAsync(ct))
            {
                if (!SupportsSimulation || _desired is not { } desired) continue;
                if (_sender.Next(desired, DateTimeOffset.Now) is { } grade) await SendGradeAsync(grade);
            }
        }
        catch (OperationCanceledException) { }
    }

    async Task SendGradeAsync(float grade)
    {
        if (!_hasControl)
        {
            var control = await SendAsync(FtmsControl.RequestControl());
            if (control != FtmsResult.Success)
            {
                StatusChanged?.Invoke($"trainer geeft geen besturing ({Describe(control)})");
                _sender.Reset(); // volgende ronde opnieuw proberen
                return;
            }
            await SendAsync(FtmsControl.StartOrResume()); // sommige trainers vereisen dit; resultaat maakt niet uit
            _hasControl = true;
            StatusChanged?.Invoke("helling actief");
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
