using FietsBridge.Core;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
using Windows.Storage.Streams;

namespace FietsBridge;

/// <summary>
/// Verbinding met één Bluetooth-apparaat: abonneert zich op de meetberichten die het aanbiedt
/// en levert ze als <see cref="Measurement"/> af. Heeft het apparaat FTMS, dan komen vermogen
/// en cadans daarvandaan en wordt Cycling Power niet ook gelezen (anders dubbele waarden).
/// </summary>
sealed class SensorConnection : IDisposable
{
    static readonly Guid IndoorBikeDataUuid = GattServices.FromShort(0x2AD2);
    static readonly Guid CyclingPowerMeasurementUuid = GattServices.FromShort(0x2A63);
    static readonly Guid HeartRateMeasurementUuid = GattServices.FromShort(0x2A37);

    readonly BluetoothLEDevice _device;
    readonly List<GattCharacteristic> _subscribed = [];
    readonly List<GattDeviceService> _services = [];
    readonly CrankCadence _crankCadence = new();
    readonly Action<Measurement> _onData;
    TrainerControl? _control;

    public string Id { get; }
    public string Name => _device.Name;
    public event Action? Disconnected;

    /// <summary>Uitleg over de hellingsbesturing, bijv. "helling actief".</summary>
    public event Action<string>? ControlStatus;

    /// <summary>Helling voor de trainer (story 1.2); apparaten zonder FTMS-besturing negeren dit.</summary>
    public void SetGrade(float gradePercent) => _control?.SetGrade(gradePercent);

    SensorConnection(string id, BluetoothLEDevice device, Action<Measurement> onData)
    {
        Id = id;
        _device = device;
        _onData = onData;
        _device.ConnectionStatusChanged += (d, _) =>
        {
            if (d.ConnectionStatus == BluetoothConnectionStatus.Disconnected) Disconnected?.Invoke();
        };
    }

    /// <summary>Verbindt en abonneert; gooit <see cref="InvalidOperationException"/> met een Nederlandse uitleg bij falen.</summary>
    public static async Task<SensorConnection> ConnectAsync(string id, Action<Measurement> onData)
    {
        var address = DeviceAddress.Parse(id) ?? throw new InvalidOperationException($"Ongeldig adres: {id}");
        var device = await BluetoothLEDevice.FromBluetoothAddressAsync(address)
            ?? throw new InvalidOperationException("Apparaat niet gevonden. Staat het aan en in de buurt?");

        var connection = new SensorConnection(id, device, onData);
        try
        {
            await connection.SubscribeAsync();
            return connection;
        }
        catch
        {
            connection.Dispose();
            throw;
        }
    }

    async Task SubscribeAsync()
    {
        var result = await _device.GetGattServicesAsync(BluetoothCacheMode.Uncached);
        if (result.Status != GattCommunicationStatus.Success)
            throw new InvalidOperationException($"Services lezen mislukt ({result.Status}).");
        _services.AddRange(result.Services);

        var byUuid = result.Services.GroupBy(s => s.Uuid).ToDictionary(g => g.Key, g => g.First());
        bool hasFtms = byUuid.ContainsKey(GattServices.FitnessMachine);

        if (hasFtms)
        {
            await TrySubscribeAsync(byUuid[GattServices.FitnessMachine], IndoorBikeDataUuid, OnIndoorBikeData);
            _control = await TrainerControl.CreateAsync(byUuid[GattServices.FitnessMachine]);
            if (_control is not null) _control.StatusChanged += message => ControlStatus?.Invoke(message);
        }
        else if (byUuid.TryGetValue(GattServices.CyclingPower, out var cps))
            await TrySubscribeAsync(cps, CyclingPowerMeasurementUuid, OnCyclingPower);

        if (byUuid.TryGetValue(GattServices.HeartRate, out var hr))
            await TrySubscribeAsync(hr, HeartRateMeasurementUuid, OnHeartRate);

        if (_subscribed.Count == 0)
            throw new InvalidOperationException("Geen vermogens-, cadans- of hartslagmetingen gevonden op dit apparaat.");
    }

    async Task TrySubscribeAsync(GattDeviceService service, Guid characteristicUuid, Action<byte[]> handler)
    {
        var chars = await service.GetCharacteristicsForUuidAsync(characteristicUuid, BluetoothCacheMode.Uncached);
        var characteristic = chars.Status == GattCommunicationStatus.Success ? chars.Characteristics.FirstOrDefault() : null;
        if (characteristic is null) return;

        characteristic.ValueChanged += (_, args) => handler(ToBytes(args.CharacteristicValue));
        var status = await characteristic.WriteClientCharacteristicConfigurationDescriptorAsync(
            GattClientCharacteristicConfigurationDescriptorValue.Notify);
        if (status == GattCommunicationStatus.Success) _subscribed.Add(characteristic);
        else Console.WriteLine($"Abonneren op {characteristicUuid} mislukt: {status}");
    }

    void OnIndoorBikeData(byte[] data)
    {
        var m = IndoorBikeData.Parse(data);
        if (m != default) _onData(m);
    }

    void OnCyclingPower(byte[] data)
    {
        var cp = CyclingPowerMeasurement.Parse(data);
        float? cadence = cp.Crank is { } crank ? _crankCadence.Update(crank, DateTimeOffset.Now) : null;
        _onData(new Measurement(cp.PowerWatts, cadence));
    }

    void OnHeartRate(byte[] data)
    {
        if (HeartRateMeasurement.Parse(data) is { } bpm) _onData(new Measurement(HeartRateBpm: bpm));
    }

    static byte[] ToBytes(IBuffer buffer)
    {
        var bytes = new byte[buffer.Length];
        using var reader = DataReader.FromBuffer(buffer);
        reader.ReadBytes(bytes);
        return bytes;
    }

    public void Dispose()
    {
        _control?.Dispose();
        _subscribed.Clear();
        foreach (var service in _services) service.Dispose();
        _services.Clear();
        _device.Dispose(); // laat Windows de verbinding sluiten
    }
}
