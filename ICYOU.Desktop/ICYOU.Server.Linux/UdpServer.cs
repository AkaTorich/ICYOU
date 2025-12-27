using System.Net;
using System.Net.Sockets;
using System.Text;
using ICYOU.Core.Protocol;
using Newtonsoft.Json;

namespace ICYOU.Server;

public class UdpServer
{
    private readonly UdpClient _udpClient;
    private readonly int _port;
    private bool _running;
    private readonly Dictionary<long, ClientInfo> _clients = new();
    private readonly object _clientsLock = new();
    
    public event EventHandler<(Packet Packet, IPEndPoint EndPoint)>? PacketReceived;
    
    public UdpServer(int port)
    {
        _port = port;
        _udpClient = new UdpClient(port);
    }
    
    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        _running = true;
        Console.WriteLine($"UDP Server started on port {_port}");
        
        while (_running && !cancellationToken.IsCancellationRequested)
        {
            try
            {
                var result = await _udpClient.ReceiveAsync(cancellationToken);
                _ = ProcessPacketAsync(result.Buffer, result.RemoteEndPoint);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error receiving packet: {ex.Message}");
            }
        }
    }
    
    private async Task ProcessPacketAsync(byte[] data, IPEndPoint endPoint)
    {
        try
        {
            var packet = Packet.DeserializeWithoutLength(data);
            if (packet == null)
            {
                Console.WriteLine($"Invalid packet from {endPoint}");
                return;
            }
            
            PacketReceived?.Invoke(this, (packet, endPoint));
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error processing packet: {ex.Message}");
        }
    }
    
    public async Task SendAsync(Packet packet, IPEndPoint endPoint)
    {
        var json = JsonConvert.SerializeObject(packet);
        var data = Encoding.UTF8.GetBytes(json);
        await _udpClient.SendAsync(data, data.Length, endPoint);
    }
    
    public async Task SendToUserAsync(Packet packet, long userId)
    {
        lock (_clientsLock)
        {
            if (_clients.TryGetValue(userId, out var client))
            {
                Console.WriteLine($"[SERVER] SendToUserAsync: Отправка пакета {packet.Type} пользователю {userId} на {client.EndPoint}");
                _ = SendAsync(packet, client.EndPoint);
            }
            else
            {
                Console.WriteLine($"[SERVER] SendToUserAsync: ОШИБКА - Пользователь {userId} не найден в списке подключенных клиентов! Всего клиентов: {_clients.Count}");
                foreach (var kvp in _clients)
                {
                    Console.WriteLine($"[SERVER]   Зарегистрированный клиент: UserId={kvp.Key}, EndPoint={kvp.Value.EndPoint}");
                }
            }
        }
    }
    
    public async Task BroadcastAsync(Packet packet, IEnumerable<long> userIds)
    {
        foreach (var userId in userIds)
        {
            await SendToUserAsync(packet, userId);
        }
    }
    
    public void RegisterClient(long userId, IPEndPoint endPoint, string sessionToken)
    {
        lock (_clientsLock)
        {
            _clients[userId] = new ClientInfo
            {
                UserId = userId,
                EndPoint = endPoint,
                SessionToken = sessionToken,
                LastActivity = DateTime.UtcNow
            };
        }
    }
    
    public void UnregisterClient(long userId)
    {
        lock (_clientsLock)
        {
            _clients.Remove(userId);
        }
    }
    
    public ClientInfo? GetClient(long userId)
    {
        lock (_clientsLock)
        {
            return _clients.TryGetValue(userId, out var client) ? client : null;
        }
    }
    
    public List<long> GetOnlineUserIds()
    {
        lock (_clientsLock)
        {
            return _clients.Keys.ToList();
        }
    }
    
    public void Stop()
    {
        _running = false;
        _udpClient.Close();
    }
}

public class ClientInfo
{
    public long UserId { get; set; }
    public IPEndPoint EndPoint { get; set; } = null!;
    public string SessionToken { get; set; } = string.Empty;
    public DateTime LastActivity { get; set; }
}

