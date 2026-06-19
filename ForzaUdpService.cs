using System.Net;
using System.Net.Sockets;

namespace ForzaRGB;

public class ForzaUdpService : IDisposable
{
    private readonly int _port;
    private UdpClient? _udpClient;
    private CancellationTokenSource? _cts;

    public event Action<ForzaSledPacket>? OnPacketReceived;
    public event Action? OnConnectionLost;

    public ForzaUdpService(int port = 7777)
    {
        _port = port;
    }

    public void Start()
    {
        _cts       = new CancellationTokenSource();
        _udpClient = new UdpClient(_port);

        Console.WriteLine($"[UDP] Listening on port {_port}...");
        Console.WriteLine($"[UDP] In FH6: Settings → HUD and Gameplay → Telemetry → IP 127.0.0.1 → Port {_port}");

        _ = Task.Run(() => ReceiveLoop(_cts.Token));
    }

    private async Task ReceiveLoop(CancellationToken ct)
    {
        var lastPacketTime = DateTime.Now;
        bool wasConnected  = false;

        while (!ct.IsCancellationRequested)
        {
            try
            {
                _udpClient!.Client.ReceiveTimeout = 100;
                var result = await _udpClient.ReceiveAsync(ct);
                lastPacketTime = DateTime.Now;

                if (!wasConnected)
                {
                    Console.WriteLine("[UDP] Receiving telemetry from Forza!");
                    wasConnected = true;
                }

                if (result.Buffer.Length < 324) continue;

                var packet = ForzaPacketExtensions.Parse(result.Buffer);
                OnPacketReceived?.Invoke(packet);
            }
            catch (SocketException ex) when (ex.SocketErrorCode == SocketError.TimedOut)
            {
                if (wasConnected && (DateTime.Now - lastPacketTime).TotalSeconds > 2)
                {
                    Console.WriteLine("[UDP] No telemetry (menu/pause).");
                    wasConnected = false;
                    OnConnectionLost?.Invoke();
                }
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex) { Console.WriteLine($"[UDP] Error: {ex.Message}"); }
        }
    }

    public void Dispose()
    {
        _cts?.Cancel();
        _udpClient?.Dispose();
    }
}
