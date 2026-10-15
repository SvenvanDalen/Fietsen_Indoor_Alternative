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
            if (_clients.IsEmpty) scanner.Stop();
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
            default:
                Console.WriteLine($"Onbekend bericht genegeerd: {message}");
                break;
        }
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
