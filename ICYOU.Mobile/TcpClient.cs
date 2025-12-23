using System.Net;
using System.Net.Sockets;
using System.Text;
using ICYOU.Core.Protocol;
using Newtonsoft.Json;

namespace ICYOU.Mobile;

public class TcpClient
{
    private System.Net.Sockets.TcpClient? _client;
    private NetworkStream? _stream;
    private readonly string _serverHost;
    private readonly int _serverPort;
    private bool _running;
    private readonly Dictionary<long, TaskCompletionSource<Packet>> _pendingRequests = new();
    private readonly object _requestsLock = new();
    
    public event EventHandler<Packet>? PacketReceived;
    public event EventHandler? Disconnected;
    
    public string? SessionToken { get; set; }
    public long UserId { get; set; }
    
    public TcpClient(string serverHost, int serverPort)
    {
        _serverHost = serverHost;
        _serverPort = serverPort;
    }
    
    public void Connect()
    {
        try
        {
            _client = new System.Net.Sockets.TcpClient();
            
            // Подключение с таймаутом
            var connectTask = _client.ConnectAsync(_serverHost, _serverPort);
            if (!connectTask.Wait(TimeSpan.FromSeconds(10)))
            {
                _client.Close();
                throw new TimeoutException("Таймаут подключения к серверу");
            }
            _stream = _client.GetStream();
            _running = true;
            Task.Run(ReceiveLoop);
            Task.Run(PingLoop);
        }
        catch (Exception ex)
        {
            _running = false;
            try
            {
                _stream?.Dispose();
            }
            catch { }
            try
            {
                _client?.Close();
                _client?.Dispose();
            }
            catch { }
            _stream = null;
            _client = null;
            throw;
        }
    }
    
    private async Task ReceiveLoop()
    {
        var buffer = new byte[4]; // Для длины пакета
        
        while (_running && _client != null && _client.Connected && _stream != null)
        {
            try
            {
                // Читаем длину пакета (4 байта)
                int bytesRead = 0;
                while (bytesRead < 4)
                {
                    var read = await _stream.ReadAsync(buffer, bytesRead, 4 - bytesRead);
                    if (read == 0)
                    {
                        // Сервер отключился
                        Disconnect();
                        break;
                    }
                    bytesRead += read;
                }
                
                if (bytesRead < 4)
                    break;
                
                // Читаем длину пакета (little-endian, как в Windows)
                var packetLength = BitConverter.ToInt32(buffer, 0);
                
                if (packetLength <= 0 || packetLength > 10 * 1024 * 1024) // Максимум 10MB
                {
                    Services.DebugLog.Write($"[CLIENT] Invalid packet length: {packetLength}");
                    Console.WriteLine($"Invalid packet length: {packetLength}");
                    break;
                }
                
                // Читаем сам пакет
                var packetData = new byte[packetLength];
                bytesRead = 0;
                while (bytesRead < packetLength)
                {
                    var read = await _stream.ReadAsync(packetData, bytesRead, packetLength - bytesRead);
                    if (read == 0)
                    {
                        Disconnect();
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
                Services.DebugLog.Write($"[CLIENT] Ошибка приема данных: {ex.Message}");
                Console.WriteLine($"Receive error: {ex.Message}");
                Disconnect();
                break;
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
        if (_stream == null || _client == null || !_client.Connected)
        {
            Services.DebugLog.Write($"[CLIENT] SendAsync: Поток не готов (Connected={_client?.Connected})");
            return;
        }
            
        packet.SessionToken = SessionToken;
        packet.UserId = UserId;
        
        var data = packet.Serialize();
        Services.DebugLog.Write($"[CLIENT] Отправка пакета {packet.Type}, размер={data.Length}");
        await _stream.WriteAsync(data, 0, data.Length);
        await _stream.FlushAsync();
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
        try
        {
            _stream?.Close();
            _client?.Close();
        }
        catch { }
        Disconnected?.Invoke(this, EventArgs.Empty);
    }
}

