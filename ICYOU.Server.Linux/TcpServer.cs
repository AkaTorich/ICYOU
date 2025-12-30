using System.Net;
using System.Net.Sockets;
using System.Text;
using ICYOU.Core.Protocol;
using Newtonsoft.Json;

namespace ICYOU.Server;

public class TcpServer
{
    private readonly TcpListener _listener;
    private readonly int _port;
    private bool _running;
    private readonly Dictionary<long, ClientConnection> _clients = new();
    private readonly object _clientsLock = new();
    
    public event EventHandler<(Packet Packet, ClientConnection Connection)>? PacketReceived;
    
    public TcpServer(int port)
    {
        _port = port;
        _listener = new TcpListener(IPAddress.Any, port);
    }
    
    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        _running = true;
        _listener.Start();
        Console.WriteLine($"TCP Server started on port {_port}");
        
        while (_running && !cancellationToken.IsCancellationRequested)
        {
            try
            {
                var tcpClient = await _listener.AcceptTcpClientAsync(cancellationToken);
                _ = HandleClientAsync(tcpClient, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error accepting client: {ex.Message}");
            }
        }
    }
    
    private async Task HandleClientAsync(TcpClient tcpClient, CancellationToken cancellationToken)
    {
        var connection = new ClientConnection { TcpClient = tcpClient };
        var stream = tcpClient.GetStream();
        var buffer = new byte[4]; // Для длины пакета
        var remoteEp = tcpClient.Client.RemoteEndPoint?.ToString() ?? "unknown";
        
        Console.WriteLine($"[SERVER] Новое подключение от {remoteEp}");
        
        try
        {
            while (tcpClient.Connected && !cancellationToken.IsCancellationRequested)
            {
                // Читаем длину пакета (4 байта)
                int bytesRead = 0;
                while (bytesRead < 4)
                {
                    var read = await stream.ReadAsync(buffer, bytesRead, 4 - bytesRead, cancellationToken);
                    if (read == 0)
                    {
                        // Клиент отключился
                        Console.WriteLine($"[SERVER] Клиент {remoteEp} отключился (read=0 при чтении длины)");
                        break;
                    }
                    bytesRead += read;
                }
                
                if (bytesRead < 4)
                    break;
                
                var packetLength = BitConverter.ToInt32(buffer, 0);
                Console.WriteLine($"[SERVER] Получен пакет от {remoteEp}, длина={packetLength}");
                
                if (packetLength <= 0 || packetLength > 10 * 1024 * 1024) // Максимум 10MB
                {
                    Console.WriteLine($"[SERVER] Invalid packet length: {packetLength}");
                    break;
                }
                
                // Читаем сам пакет
                var packetData = new byte[packetLength];
                bytesRead = 0;
                while (bytesRead < packetLength)
                {
                    var read = await stream.ReadAsync(packetData, bytesRead, packetLength - bytesRead, cancellationToken);
                    if (read == 0)
                    {
                        Console.WriteLine($"[SERVER] Клиент {remoteEp} отключился (read=0 при чтении данных)");
                        break;
                    }
                    bytesRead += read;
                }
                
                if (bytesRead < packetLength)
                    break;
                
                // Создаем полный массив с длиной для Deserialize
                var fullPacketData = new byte[4 + packetLength];
                BitConverter.GetBytes(packetLength).CopyTo(fullPacketData, 0);
                packetData.CopyTo(fullPacketData, 4);
                
                var packet = Packet.Deserialize(fullPacketData);
                if (packet != null)
                {
                    Console.WriteLine($"[SERVER] Пакет десериализован: Type={packet.Type}, SequenceId={packet.SequenceId}");
                    PacketReceived?.Invoke(this, (packet, connection));
                }
                else
                {
                    Console.WriteLine($"[SERVER] ОШИБКА: Не удалось десериализовать пакет от {remoteEp}");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[SERVER] Error handling client {remoteEp}: {ex.Message}");
            Console.WriteLine($"[SERVER] StackTrace: {ex.StackTrace}");
        }
        finally
        {
            // Удаляем клиента из списка
            if (connection.UserId > 0)
            {
                lock (_clientsLock)
                {
                    _clients.Remove(connection.UserId);
                }
            }
            
            tcpClient.Close();
        }
    }
    
    public async Task SendAsync(Packet packet, ClientConnection connection)
    {
        try
        {
            if (!connection.TcpClient.Connected)
            {
                Console.WriteLine($"[SERVER] SendAsync: Клиент не подключен (UserId={connection.UserId})");
                return;
            }
                
            var data = packet.Serialize();
            var stream = connection.TcpClient.GetStream();
            await stream.WriteAsync(data, 0, data.Length);
            await stream.FlushAsync();
            Console.WriteLine($"[SERVER] SendAsync: Пакет {packet.Type} отправлен (UserId={connection.UserId}, Size={data.Length})");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[SERVER] Error sending packet {packet.Type} to UserId={connection.UserId}: {ex.Message}");
        }
    }
    
    public async Task SendToUserAsync(Packet packet, long userId)
    {
        ClientConnection? connection = null;
        lock (_clientsLock)
        {
            if (_clients.TryGetValue(userId, out connection))
            {
                Console.WriteLine($"[SERVER] SendToUserAsync: Отправка пакета {packet.Type} пользователю {userId}");
            }
            else
            {
                Console.WriteLine($"[SERVER] SendToUserAsync: ОШИБКА - Пользователь {userId} не найден в списке подключенных клиентов! Всего клиентов: {_clients.Count}");
                foreach (var kvp in _clients)
                {
                    Console.WriteLine($"[SERVER]   Зарегистрированный клиент: UserId={kvp.Key}");
                }
            }
        }
        
        if (connection != null)
        {
            await SendAsync(packet, connection);
        }
    }
    
    public async Task BroadcastAsync(Packet packet, IEnumerable<long> userIds)
    {
        foreach (var userId in userIds)
        {
            await SendToUserAsync(packet, userId);
        }
    }
    
    public void RegisterClient(long userId, ClientConnection connection, string sessionToken)
    {
        lock (_clientsLock)
        {
            connection.UserId = userId;
            connection.SessionToken = sessionToken;
            connection.LastActivity = DateTime.UtcNow;
            _clients[userId] = connection;
            Console.WriteLine($"[SERVER] Клиент зарегистрирован: UserId={userId}, Всего клиентов: {_clients.Count}");
        }
    }
    
    public void UnregisterClient(long userId)
    {
        lock (_clientsLock)
        {
            _clients.Remove(userId);
        }
    }
    
    public ClientConnection? GetClient(long userId)
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
        _listener.Stop();
        
        lock (_clientsLock)
        {
            foreach (var client in _clients.Values)
            {
                try
                {
                    client.TcpClient.Close();
                }
                catch { }
            }
            _clients.Clear();
        }
    }
}

public class ClientConnection
{
    public TcpClient TcpClient { get; set; } = null!;
    public long UserId { get; set; }
    public string SessionToken { get; set; } = string.Empty;
    public DateTime LastActivity { get; set; }
}

