using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace FietsApp.Bluetooth
{
    /// <summary>
    /// Verbinding met FietsBridge via een lokale WebSocket. Verbindt automatisch (opnieuw)
    /// en levert berichten af op de hoofdthread, zodat luisteraars Unity-objecten mogen aanraken.
    /// </summary>
    public sealed class BridgeClient : MonoBehaviour
    {
        [SerializeField] string url = "ws://localhost:8765/";
        [SerializeField, Min(0.5f)] float retrySeconds = 2f;

        readonly ConcurrentQueue<BridgeMessage> _inbox = new ConcurrentQueue<BridgeMessage>();
        CancellationTokenSource _cts;
        ClientWebSocket _socket;
        volatile bool _connected;
        bool _wasConnected;

        public bool IsConnected => _connected;
        public StatusMessage LastStatus { get; private set; }

        public event Action<bool> ConnectionChanged;
        public event Action<StatusMessage> StatusReceived;
        public event Action<DevicesMessage> DevicesReceived;
        public event Action<DataMessage> DataReceived;
        public event Action<ConnectionMessage> ConnectionReceived;

        void OnEnable()
        {
            _cts = new CancellationTokenSource();
            _ = ConnectLoopAsync(_cts.Token);
        }

        void OnDisable()
        {
            _cts.Cancel();
            _socket?.Abort();
            _connected = false;
        }

        void Update()
        {
            if (_wasConnected != _connected)
            {
                _wasConnected = _connected;
                if (!_connected) LastStatus = null;
                ConnectionChanged?.Invoke(_connected);
            }

            while (_inbox.TryDequeue(out var message))
            {
                switch (message)
                {
                    case StatusMessage status:
                        LastStatus = status;
                        StatusReceived?.Invoke(status);
                        break;
                    case DevicesMessage devices:
                        DevicesReceived?.Invoke(devices);
                        break;
                    case DataMessage data:
                        DataReceived?.Invoke(data);
                        break;
                    case ConnectionMessage connection:
                        ConnectionReceived?.Invoke(connection);
                        break;
                }
            }
        }

        public void SetScanning(bool enabled, bool all) => _ = SendAsync(BridgeMessages.Scan(enabled, all));

        public void Connect(string id) => _ = SendAsync(BridgeMessages.Connect(id));

        public void Disconnect(string id) => _ = SendAsync(BridgeMessages.Disconnect(id));

        async Task SendAsync(string text)
        {
            var socket = _socket;
            if (!_connected || socket == null) return;
            try
            {
                await socket.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(text)),
                    WebSocketMessageType.Text, true, _cts.Token);
            }
            catch (Exception e) when (e is WebSocketException || e is OperationCanceledException)
            {
                // Verbinding valt weg; de ontvangstlus merkt het en verbindt opnieuw.
            }
        }

        async Task ConnectLoopAsync(CancellationToken ct)
        {
            var buffer = new byte[16 * 1024];
            while (!ct.IsCancellationRequested)
            {
                using (var socket = new ClientWebSocket())
                {
                    _socket = socket;
                    try
                    {
                        await socket.ConnectAsync(new Uri(url), ct);
                        _connected = true;
                        while (socket.State == WebSocketState.Open)
                        {
                            var text = await ReceiveTextAsync(socket, buffer, ct);
                            if (text == null) break;
                            var message = BridgeMessages.Parse(text);
                            if (message != null) _inbox.Enqueue(message);
                        }
                    }
                    catch (Exception e) when (e is WebSocketException || e is OperationCanceledException)
                    {
                        // Brug draait niet (nog) of is gestopt.
                    }
                    finally
                    {
                        _connected = false;
                        _socket = null;
                    }
                }

                try { await Task.Delay(TimeSpan.FromSeconds(retrySeconds), ct); }
                catch (OperationCanceledException) { return; }
            }
        }

        static async Task<string> ReceiveTextAsync(ClientWebSocket socket, byte[] buffer, CancellationToken ct)
        {
            using (var stream = new MemoryStream())
            {
                WebSocketReceiveResult result;
                do
                {
                    result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), ct);
                    if (result.MessageType == WebSocketMessageType.Close) return null;
                    stream.Write(buffer, 0, result.Count);
                } while (!result.EndOfMessage);

                return Encoding.UTF8.GetString(stream.ToArray());
            }
        }
    }
}
