using FietsBridge.Core;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.Advertisement;
using Windows.Devices.Radios;

namespace FietsBridge;

/// <summary>Zoekt Bluetooth LE-apparaten via de Windows-API en meldt ze aan het register.</summary>
sealed class BleScanner
{
    readonly DeviceRegistry _registry;
    readonly BluetoothLEAdvertisementWatcher _watcher = new()
    {
        // Actief zoeken vraagt ook het scan-antwoord op, waar de naam vaak in staat.
        ScanningMode = BluetoothLEScanningMode.Active,
    };

    // Eigen vlag: _watcher.Status wordt pas later "Started", want de watcher start asynchroon.
    volatile bool _scanning;

    public BleScanner(DeviceRegistry registry)
    {
        _registry = registry;
        _watcher.Received += OnReceived;
        // Stopt de watcher met een fout (bijvoorbeeld Bluetooth uitgezet), dan zoeken we niet meer.
        _watcher.Stopped += (_, args) =>
        {
            if (args.Error != BluetoothError.Success) _scanning = false;
        };
    }

    public bool IsScanning => _scanning;

    public void Start()
    {
        if (_scanning) return;
        _registry.Clear();
        _watcher.Start();
        _scanning = true;
    }

    public void Stop()
    {
        if (!_scanning) return;
        _watcher.Stop();
        _scanning = false;
    }

    void OnReceived(BluetoothLEAdvertisementWatcher sender, BluetoothLEAdvertisementReceivedEventArgs args) =>
        _registry.Report(
            args.BluetoothAddress,
            args.Advertisement.LocalName,
            args.RawSignalStrengthInDBm,
            args.Advertisement.ServiceUuids,
            args.Timestamp);

    public static async Task<BluetoothState> GetStateAsync()
    {
        var adapter = await BluetoothAdapter.GetDefaultAsync();
        if (adapter is null || !adapter.IsLowEnergySupported) return BluetoothState.Unavailable;
        var radio = await adapter.GetRadioAsync();
        return radio?.State == RadioState.On ? BluetoothState.On : BluetoothState.Off;
    }
}
