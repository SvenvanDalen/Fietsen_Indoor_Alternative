using FietsBridge;
using FietsBridge.Core;

// Brug tussen de Windows-Bluetooth-API en Unity (story 1.1). Start met: dotnet run --project Bridge/FietsBridge
const int DefaultPort = 8765;
int port = args.Length > 0 && int.TryParse(args[0], out var p) ? p : DefaultPort;

var registry = new DeviceRegistry();
var scanner = new BleScanner(registry);
var server = new BridgeServer(port, registry, scanner);

using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };

Console.WriteLine($"FietsBridge luistert op {server.Url}  (Bluetooth: {await BleScanner.GetStateAsync()})");
Console.WriteLine("Stoppen met Ctrl+C.");

await server.RunAsync(cts.Token);
scanner.Stop();
