using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// UPose-compatible UDP server.
/// Provides legacy API expected by UPose.cs:
/// - Connect()
/// - StartListeningAsync()
/// - HasMessage()
/// - GetMessage()
/// - Disconnect()
///
/// Internals:
/// - Background thread receives UDP
/// - Messages are queued thread-safely
/// - Clean shutdown without ObjectDisposedException spam
/// </summary>
public class ServerUDP
{
    private readonly string host;
    private readonly int port;

    private UdpClient udpClient;
    private Thread listenThread;
    private volatile bool running;

    private readonly ConcurrentQueue<string> messageQueue = new ConcurrentQueue<string>();

    // Optional: store last message
    private volatile string lastMessage = null;

    public ServerUDP(string host, int port)
    {
        this.host = host;
        this.port = port;
    }

    /// <summary>
    /// Legacy: create/bind socket. No args per UPose.cs usage.
    /// Safe to call multiple times.
    /// </summary>
    public void Connect()
    {
        if (udpClient != null) return;

        try
        {
            // Bind to Any:port. `host` is typically not needed for binding.
            udpClient = new UdpClient(port);
            udpClient.Client.ReceiveTimeout = 1000; // allows responsive exit checks
            Debug.Log($"[ServerUDP] Connected (listening) on UDP port {port}");
        }
        catch (Exception e)
        {
            Debug.LogError($"[ServerUDP] Failed to bind UDP port {port}: {e}");
            udpClient = null;
        }
    }

    /// <summary>
    /// Legacy: starts background listening.
    /// UPose expects this method exists. We'll start a thread and return a completed Task.
    /// Safe to call multiple times.
    /// </summary>
    public Task StartListeningAsync()
    {
        if (running) return Task.CompletedTask;

        if (udpClient == null)
        {
            // In case caller forgot to Connect() first
            Connect();
            if (udpClient == null) return Task.CompletedTask;
        }

        running = true;
        listenThread = new Thread(ListenLoop) { IsBackground = true };
        listenThread.Start();

        return Task.CompletedTask;
    }

    /// <summary>
    /// Legacy: whether there is at least one message waiting.
    /// </summary>
    public bool HasMessage()
    {
        return !messageQueue.IsEmpty;
    }

    /// <summary>
    /// Legacy: pop one message. Returns null if none.
    /// </summary>
    public string GetMessage()
    {
        if (messageQueue.TryDequeue(out var msg))
            return msg;
        return null;
    }

    /// <summary>
    /// Legacy: stop listening and release resources.
    /// Safe to call multiple times.
    /// </summary>
    public void Disconnect()
    {
        running = false;

        try
        {
            udpClient?.Close();
            udpClient?.Dispose();
        }
        catch { /* ignore */ }
        finally
        {
            udpClient = null;
        }

        try
        {
            if (listenThread != null && listenThread.IsAlive)
                listenThread.Join(200);
        }
        catch { /* ignore */ }
        finally
        {
            listenThread = null;
        }

        // Optional: clear queued messages
        while (messageQueue.TryDequeue(out _)) { }

        Debug.Log("[ServerUDP] Disconnected");
    }

    private void ListenLoop()
    {
        IPEndPoint remoteEP = new IPEndPoint(IPAddress.Any, 0);

        while (running)
        {
            try
            {
                byte[] data = udpClient.Receive(ref remoteEP);
                if (data == null || data.Length == 0) continue;

                string msg = Encoding.UTF8.GetString(data);

                lastMessage = msg;
                messageQueue.Enqueue(msg);
            }
            catch (SocketException se)
            {
                if (!running) break;

                // Timeout is expected; lets us check running periodically
                if (se.SocketErrorCode == SocketError.TimedOut)
                    continue;

                // Common benign errors on shutdown
                if (se.SocketErrorCode == SocketError.Interrupted ||
                    se.SocketErrorCode == SocketError.NotSocket ||
                    se.SocketErrorCode == SocketError.ConnectionReset)
                    continue;

                Debug.LogWarning($"[ServerUDP] SocketException: {se.SocketErrorCode} {se.Message}");
            }
            catch (ObjectDisposedException)
            {
                // Normal if socket disposed during shutdown
                break;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[ServerUDP] ListenLoop exception: {e}");
            }
        }
    }
}