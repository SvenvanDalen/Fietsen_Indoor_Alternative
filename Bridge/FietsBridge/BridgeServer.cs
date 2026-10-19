using System.Collections.Concurrent;
using System.Net;
using System.Net.WebSockets;
using System.Text;
using FietsBridge.Core;

namespace FietsBridge;

/// <summary>
/// Lokale WebSocket-server voor Unity. Ontvangt opdrachten, en stuurt tijdens het zoeken
/// elke seconde de apparatenlijst naar alle verbonden clients.
/// </summary>
sealed class BridgeServer(int port, DeviceRegistry registry, BleScanner scanner)
{
    readonly ConcurrentDictionary<WebSocket, byte> _clients = new();
    readonly ConcurrentDictionary<string, SensorConnection> _sensors = new();
    // Onverwacht weggevallen apparaten waarvoor de brug opnieuw probeert te verbinden (story 1.5).
    readonly ConcurrentDictionary<string, CancellationTokenSource> _reconnecting = new();
    readonly SemaphoreSlim _sendLock = new(1, 1);
    volatile bool _includeUnknown;

    public string Url => $"ws://localhost:{port}/";

    public async Task RunAsync(CancellationToken ct)
    {
        using var listener = new HttpListener();
        // "localhost" in plaats van "+" werkt zonder administratorrechten.
        listener.Prefixes.Add($"http://localhost:{port}/");
        listener.Start();
        ct.Register(listener.Stop);

        _ = BroadcastLoopAsync(ct);

        while (!ct.IsCancellationRequested)
        {
            HttpListenerContext context;
            try { context = await listener.GetContextAsync(); }
            catch (HttpListenerException) when (ct.IsCancellationRequested) { break; }
            catch (ObjectDisposedException) { break; }

            if (!context.Request.IsWebSocketRequest)
            {
                context.Response.StatusCode = 400;
                context.Response.Close();
                continue;
            }
            _ = HandleClientAsync(context, ct);
        }
    }

    async Task HandleClientAsync(HttpListenerContext context, CancellationToken ct)
    {
        var socket = (await context.AcceptWebSocketAsync(subProtocol: null)).WebSocket;
        _clients[socket] = 0;
        Console.WriteLine($"Unity verbonden ({_clients.Count} client(s))");

        try
        {
            await SendAsync(socket, Protocol.Status(await BleScanner.GetStateAsync(), scanner.IsScanning), ct);
            foreach (var sensor in _sensors.Values)
                await SendAsync(socket, Protocol.Connection(sensor.Id, sensor.Name, ConnectionState.Connected), ct);

            var buffer = new byte[4096];
            while (socket.State == WebSocketState.Open && !ct.IsCancellationRequested)
            {
                var message = await ReceiveTextAsync(socket, buffer, ct);
                if (message is null) break;
                await HandleCommandAsync(message, ct);
            }
        }
        catch (Exception e) when (e is WebSocketException or OperationCanceledException)
        {
            // Client weg of brug stopt: verder opruimen hieronder.
        }
        finally
        {
            _clients.TryRemove(socket, out _);
            socket.Dispose();
            Console.WriteLine($"Unity losgekoppeld ({_clients.Count} client(s))");
            if (_clients.IsEmpty)
            {
                // Unity is weg: apparaten vrijgeven zodat andere apps ze kunnen gebruiken.
                scanner.Stop();
                foreach (var id in _reconnecting.Keys) StopReconnecting(id);
                foreach (var id in _sensors.Keys) Disconnect(id);
            }
        }
    }

    async Task HandleCommandAsync(string message, CancellationToken ct)
    {
        switch (Protocol.Parse(message))
        {
            case ScanCommand scan:
                _includeUnknown = scan.All;
                var state = await BleScanner.GetStateAsync();
                if (scan.Enabled && state == BluetoothState.On) scanner.Start();
                else scanner.Stop();
                Console.WriteLine(scanner.IsScanning ? "Zoeken gestart" : "Zoeken gestopt");
                await BroadcastAsync(Protocol.Status(state, scanner.IsScanning), ct);
                break;
            case ConnectCommand connect:
                await ConnectAsync(connect.Id, ct);
                break;
            case GradeCommand grade:
                if (_sensors.TryGetValue(grade.Id, out var sensor)) sensor.SetGrade(grade.Grade);
                break;
            case PowerCommand power:
                if (_sensors.TryGetValue(power.Id, out var trainer)) trainer.SetTargetPower(power.Watts > 0 ? power.Watts : null);
                break;
            case DisconnectCommand disconnect:
                if ((StopReconnecting(disconnect.Id) ?? Disconnect(disconnect.Id)) is { } name)
                    await BroadcastAsync(Protocol.Connection(disconnect.Id, name, ConnectionState.Disconnected), ct);
                break;
            default:
                Console.WriteLine($"Onbekend bericht genegeerd: {message}");
                break;
        }
    }

    async Task ConnectAsync(string id, CancellationToken ct)
    {
        if (_sensors.ContainsKey(id) || _reconnecting.ContainsKey(id)) return;
        string name = registry.Snapshot(DateTimeOffset.Now, includeUnknown: true).FirstOrDefault(d => d.Id == id)?.Name ?? "";
        Console.WriteLine($"Verbinden met {id} {name}");
        await BroadcastAsync(Protocol.Connection(id, name, ConnectionState.Connecting), ct);

        try
        {
            await AttachAsync(id, await OpenAsync(id, ct), ct);
        }
        catch (Exception e) when (e is InvalidOperationException or System.Runtime.InteropServices.COMException)
        {
            Console.WriteLine($"Verbinden met {id} mislukt: {e.Message}");
            await BroadcastAsync(Protocol.Connection(id, name, ConnectionState.Failed, e.Message), ct);
        }
    }

    Task<SensorConnection> OpenAsync(string id, CancellationToken ct) =>
        SensorConnection.ConnectAsync(id, m => _ = BroadcastAsync(Protocol.Data(id, m), ct));

    /// <summary>Neemt een verbonden apparaat op en meldt het aan Unity, die dan meteen helling en ERG-doel stuurt.</summary>
    async Task AttachAsync(string id, SensorConnection sensor, CancellationToken ct)
    {
        sensor.Disconnected += () => OnLost(id, ct);
        sensor.ControlStatus += message =>
        {
            Console.WriteLine($"{id}: {message}");
            _ = BroadcastAsync(Protocol.Connection(id, sensor.Name, ConnectionState.Connected, message), ct);
        };
        _sensors[id] = sensor;
        Console.WriteLine($"Verbonden met {id} {sensor.Name}");
        await BroadcastAsync(Protocol.Connection(id, sensor.Name, ConnectionState.Connected), ct);
    }

    /// <summary>Apparaat viel onverwacht weg (buiten bereik, trainer even zonder stroom): opnieuw proberen.</summary>
    void OnLost(string id, CancellationToken ct)
    {
        if (Disconnect(id) is not { } name) return; // al bewust ontkoppeld of al aan het herstellen
        var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        if (!_reconnecting.TryAdd(id, cts)) { cts.Dispose(); return; }
        Console.WriteLine($"Verbinding met {id} verbroken; opnieuw verbinden");
        _ = ReconnectAsync(id, name, cts.Token, ct);
    }

    /// <param name="stop">Stopt het proberen (bewust ontkoppeld, Unity weg).</param>
    /// <param name="server">Levensduur van de brug, voor de herstelde verbinding.</param>
    async Task ReconnectAsync(string id, string name, CancellationToken stop, CancellationToken server)
    {
        var ct = stop;
        var started = DateTimeOffset.Now;
        try
        {
            await BroadcastAsync(Protocol.Connection(id, name, ConnectionState.Reconnecting), ct);
            for (int attempt = 0; ReconnectSchedule.Delay(attempt, DateTimeOffset.Now - started) is { } delay; attempt++)
            {
                await Task.Delay(delay, ct);
                try
                {
                    var sensor = await OpenAsync(id, server);
                    if (ct.IsCancellationRequested || !_reconnecting.TryRemove(id, out var cts)) { sensor.Dispose(); return; }
                    cts.Dispose();
                    Console.WriteLine($"Verbinding met {id} hersteld na {(DateTimeOffset.Now - started).TotalSeconds:0.0} s");
                    await AttachAsync(id, sensor, server);
                    return;
                }
                catch (Exception e) when (e is InvalidOperationException or System.Runtime.InteropServices.COMException)
                {
                    // Nog niet terug; volgende poging.
                }
            }
            if (StopReconnecting(id) is null) return;
            Console.WriteLine($"Verbinding met {id} niet hersteld");
            await BroadcastAsync(Protocol.Connection(id, name, ConnectionState.Failed, "verbinding niet hersteld"), CancellationToken.None);
        }
        catch (OperationCanceledException) { /* bewust ontkoppeld of Unity weg */ }
    }

    /// <summary>Stopt het opnieuw verbinden; geeft het id terug (de naam kent Unity al), of null als er niets liep.</summary>
    string? StopReconnecting(string id)
    {
        if (!_reconnecting.TryRemove(id, out var cts)) return null;
        cts.Cancel();
        cts.Dispose();
        return id;
    }

    /// <summary>Verbreekt de verbinding; geeft de naam terug, of null als er geen verbinding was.</summary>
    string? Disconnect(string id)
    {
        if (!_sensors.TryRemove(id, out var sensor)) return null;
        string name = sensor.Name;
        sensor.Dispose();
        return name;
    }

    async Task BroadcastLoopAsync(CancellationToken ct)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
        try
        {
            while (await timer.WaitForNextTickAsync(ct))
            {
                if (!scanner.IsScanning) continue;
                var devices = registry.Snapshot(DateTimeOffset.Now, _includeUnknown);
                await BroadcastAsync(Protocol.Devices(devices), ct);
            }
        }
        catch (OperationCanceledException) { }
    }

    async Task BroadcastAsync(string message, CancellationToken ct)
    {
        foreach (var socket in _clients.Keys)
        {
            try { await SendAsync(socket, message, ct); }
            catch (WebSocketException) { /* client valt weg; HandleClientAsync ruimt op */ }
        }
    }

    async Task SendAsync(WebSocket socket, string message, CancellationToken ct)
    {
        await _sendLock.WaitAsync(ct);
        try
        {
            await socket.SendAsync(Encoding.UTF8.GetBytes(message), WebSocketMessageType.Text, true, ct);
        }
        finally { _sendLock.Release(); }
    }

    static async Task<string?> ReceiveTextAsync(WebSocket socket, byte[] buffer, CancellationToken ct)
    {
        using var stream = new MemoryStream();
        WebSocketReceiveResult result;
        do
        {
            result = await socket.ReceiveAsync(buffer, ct);
            if (result.MessageType == WebSocketMessageType.Close)
            {
                await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, null, ct);
                return null;
            }
            stream.Write(buffer, 0, result.Count);
        } while (!result.EndOfMessage);

        return Encoding.UTF8.GetString(stream.ToArray());
    }
}
