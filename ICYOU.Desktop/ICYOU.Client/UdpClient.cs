using System.Net;
using System.Net.Sockets;
using System.Text;
using ICYOU.Core.Protocol;
using Newtonsoft.Json;

namespace ICYOU.Client;

public class UdpClient
{
    private readonly System.Net.Sockets.UdpClient _client;
    private readonly IPEndPoint _serverEndPoint;
    private bool _running;
    private readonly Dictionary<long, TaskCompletionSource<Packet>> _pendingRequests = new();
    private readonly object _requestsLock = new();
    
    public event EventHandler<Packet>? PacketReceived;
    public event EventHandler? Disconnected;
    
    public string? SessionToken { get; set; }
    public long UserId { get; set; }
    
    public UdpClient(string serverHost, int serverPort)
    {
        _client = new System.Net.Sockets.UdpClient();
        _serverEndPoint = new IPEndPoint(IPAddress.Parse(serverHost), serverPort);
    }
    
    public void Connect()
    {
        _client.Connect(_serverEndPoint);
        _running = true;
        Task.Run(ReceiveLoop);
        Task.Run(PingLoop);
    }
    
    private async Task ReceiveLoop()
    {
        while (_running)
        {
            try
            {
                var result = await _client.ReceiveAsync();
                var json = Encoding.UTF8.GetString(result.Buffer);
                var packet = JsonConvert.DeserializeObject<Packet>(json);
                
                if (packet != null)
                {
                    // Проверяем ожидающие запросы
                    lock (_requestsLock)
                    {
                        if (_pendingRequests.TryGetValue(packet.SequenceId, out var tcs))
                        {
                            _pendingRequests.Remove(packet.SequenceId);
                            tcs.SetResult(packet);
                            continue;
                        }
                    }
                    
                    PacketReceived?.Invoke(this, packet);
                }
            }
            catch (ObjectDisposedException)
            {
                break;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Receive error: {ex.Message}");
            }
        }
    }
    
    private async Task PingLoop()
    {
        while (_running)
        {
            try
            {
                await Task.Delay(30000);
                if (_running && !string.IsNullOrEmpty(SessionToken))
                {
                    await SendAsync(new Packet(PacketType.Ping));
                }
            }
            catch
            {
            }
        }
    }
    
    public async Task SendAsync(Packet packet)
    {
        packet.SessionToken = SessionToken;
        packet.UserId = UserId;
        
        var json = JsonConvert.SerializeObject(packet);
        var data = Encoding.UTF8.GetBytes(json);
        await _client.SendAsync(data, data.Length);
    }
    
    public async Task<Packet?> SendAndWaitAsync(Packet packet, TimeSpan? timeout = null)
    {
        var tcs = new TaskCompletionSource<Packet>();
        
        lock (_requestsLock)
        {
            _pendingRequests[packet.SequenceId] = tcs;
        }
        
        await SendAsync(packet);
        
        var timeoutTask = Task.Delay(timeout ?? TimeSpan.FromSeconds(10));
        var completedTask = await Task.WhenAny(tcs.Task, timeoutTask);
        
        if (completedTask == timeoutTask)
        {
            lock (_requestsLock)
            {
                _pendingRequests.Remove(packet.SequenceId);
            }
            return null;
        }
        
        return await tcs.Task;
    }
    
    public void Disconnect()
    {
        _running = false;
        _client.Close();
        Disconnected?.Invoke(this, EventArgs.Empty);
    }
}

