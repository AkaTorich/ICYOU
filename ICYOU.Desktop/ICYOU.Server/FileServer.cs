using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;

namespace ICYOU.Server;

public class FileServer
{
    private TcpListener? _listener;
    private readonly int _port;
    private bool _running;
    private readonly string _tempPath;
    private readonly ConcurrentDictionary<string, FileInfo> _files = new();
    
    public event EventHandler<FileUploadedEventArgs>? FileUploaded;
    public event EventHandler<FileDownloadedEventArgs>? FileDownloaded;
    
    public FileServer(int port)
    {
        _port = port;
        _tempPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "temp_files");
        Directory.CreateDirectory(_tempPath);
    }
    
    public void Start()
    {
        _listener = new TcpListener(IPAddress.Any, _port);
        _listener.Start();
        _running = true;
        Task.Run(AcceptClients);
        Console.WriteLine($"[FileServer] TCP порт {_port}");
    }
    
    public void Stop()
    {
        _running = false;
        _listener?.Stop();
    }
    
    private async Task AcceptClients()
    {
        while (_running)
        {
            try
            {
                var client = await _listener!.AcceptTcpClientAsync();
                _ = Task.Run(() => HandleClientAsync(client));
            }
            catch when (!_running) { }
            catch (Exception ex)
            {
                Console.WriteLine($"[FileServer] Ошибка accept: {ex.Message}");
            }
        }
    }
    
    private async Task HandleClientAsync(TcpClient client)
    {
        var remoteEp = client.Client.RemoteEndPoint?.ToString() ?? "unknown";
        Console.WriteLine($"[FileServer] Подключение от {remoteEp}");
        
        try
        {
            client.ReceiveTimeout = 120000;
            client.SendTimeout = 120000;
            
            using var stream = client.GetStream();
            
            // Читаем команду (1 байт)
            var cmdByte = new byte[1];
            if (await ReadExactAsync(stream, cmdByte, 1) != 1)
            {
                Console.WriteLine($"[FileServer] Не удалось прочитать команду");
                return;
            }
            
            var cmd = cmdByte[0];
            Console.WriteLine($"[FileServer] Команда: {cmd}");
            
            if (cmd == 1)
            {
                await HandleUploadAsync(stream);
            }
            else if (cmd == 2)
            {
                await HandleDownloadAsync(stream);
            }
            
            Console.WriteLine($"[FileServer] Обработка завершена для {remoteEp}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[FileServer] Ошибка: {ex.Message}");
        }
        finally
        {
            try { client.Close(); } catch { }
        }
    }
    
    private async Task<int> ReadExactAsync(NetworkStream stream, byte[] buffer, int count)
    {
        int offset = 0;
        while (offset < count)
        {
            int read = await stream.ReadAsync(buffer.AsMemory(offset, count - offset));
            if (read == 0) return offset;
            offset += read;
        }
        return offset;
    }
    
    private async Task HandleUploadAsync(NetworkStream stream)
    {
        // Читаем senderId (8 байт)
        var senderIdBytes = new byte[8];
        await ReadExactAsync(stream, senderIdBytes, 8);
        var senderId = BitConverter.ToInt64(senderIdBytes);
        
        // Читаем targetUserId (8 байт)
        var targetUserIdBytes = new byte[8];
        await ReadExactAsync(stream, targetUserIdBytes, 8);
        var targetUserId = BitConverter.ToInt64(targetUserIdBytes);
        
        // Читаем chatId (8 байт)
        var chatIdBytes = new byte[8];
        await ReadExactAsync(stream, chatIdBytes, 8);
        var chatId = BitConverter.ToInt64(chatIdBytes);
        
        // Читаем длину имени файла (4 байта)
        var nameLenBytes = new byte[4];
        await ReadExactAsync(stream, nameLenBytes, 4);
        var nameLen = BitConverter.ToInt32(nameLenBytes);
        
        if (nameLen <= 0 || nameLen > 1000)
        {
            Console.WriteLine($"[FileServer] Неверная длина имени: {nameLen}");
            return;
        }
        
        // Читаем имя файла
        var nameBytes = new byte[nameLen];
        await ReadExactAsync(stream, nameBytes, nameLen);
        var fileName = System.Text.Encoding.UTF8.GetString(nameBytes);
        
        // Читаем размер файла (8 байт)
        var fileSizeBytes = new byte[8];
        await ReadExactAsync(stream, fileSizeBytes, 8);
        var fileSize = BitConverter.ToInt64(fileSizeBytes);
        
        Console.WriteLine($"[FileServer] Загрузка: {fileName} ({fileSize} байт) от {senderId} для {targetUserId}");
        
        // Генерируем ID и сохраняем файл
        var fileId = Guid.NewGuid().ToString("N");
        var tempFile = Path.Combine(_tempPath, fileId);
        
        using (var fs = new FileStream(tempFile, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true))
        {
            var buffer = new byte[8192];
            long received = 0;
            
            while (received < fileSize)
            {
                var toRead = (int)Math.Min(buffer.Length, fileSize - received);
                var read = await stream.ReadAsync(buffer.AsMemory(0, toRead));
                if (read == 0)
                {
                    Console.WriteLine($"[FileServer] Соединение закрыто на {received}/{fileSize}");
                    break;
                }
                await fs.WriteAsync(buffer.AsMemory(0, read));
                received += read;
            }
            
            Console.WriteLine($"[FileServer] Получено {received}/{fileSize} байт");
        }
        
        // Сохраняем информацию
        _files[fileId] = new FileInfo
        {
            FileId = fileId,
            SenderId = senderId,
            TargetUserId = targetUserId,
            ChatId = chatId,
            FileName = fileName,
            FileSize = fileSize,
            TempPath = tempFile,
            UploadedAt = DateTime.UtcNow
        };
        
        // Отправляем fileId клиенту
        var idBytes = System.Text.Encoding.UTF8.GetBytes(fileId);
        var lenBytes = BitConverter.GetBytes(idBytes.Length);
        await stream.WriteAsync(lenBytes);
        await stream.WriteAsync(idBytes);
        await stream.FlushAsync();
        
        Console.WriteLine($"[FileServer] Загружен: {fileName} -> {fileId}");
        
        // Уведомляем
        FileUploaded?.Invoke(this, new FileUploadedEventArgs
        {
            FileId = fileId,
            SenderId = senderId,
            TargetUserId = targetUserId,
            ChatId = chatId,
            FileName = fileName,
            FileSize = fileSize
        });
    }
    
    private async Task HandleDownloadAsync(NetworkStream stream)
    {
        // Читаем длину ID
        var idLenBytes = new byte[4];
        await ReadExactAsync(stream, idLenBytes, 4);
        var idLen = BitConverter.ToInt32(idLenBytes);
        
        if (idLen <= 0 || idLen > 100)
        {
            await stream.WriteAsync(new byte[] { 0 }); // false
            return;
        }
        
        // Читаем ID
        var idBytes = new byte[idLen];
        await ReadExactAsync(stream, idBytes, idLen);
        var fileId = System.Text.Encoding.UTF8.GetString(idBytes);
        
        if (!_files.TryGetValue(fileId, out var info) || !File.Exists(info.TempPath))
        {
            await stream.WriteAsync(new byte[] { 0 }); // false
            _files.TryRemove(fileId, out _);
            return;
        }
        
        // Отправляем success
        await stream.WriteAsync(new byte[] { 1 }); // true
        
        // Отправляем имя файла
        var nameBytes = System.Text.Encoding.UTF8.GetBytes(info.FileName);
        await stream.WriteAsync(BitConverter.GetBytes(nameBytes.Length));
        await stream.WriteAsync(nameBytes);
        
        // Отправляем размер
        await stream.WriteAsync(BitConverter.GetBytes(info.FileSize));
        
        // Отправляем данные
        using (var fs = new FileStream(info.TempPath, FileMode.Open, FileAccess.Read, FileShare.Read, 8192, true))
        {
            var buffer = new byte[8192];
            int read;
            while ((read = await fs.ReadAsync(buffer)) > 0)
            {
                await stream.WriteAsync(buffer.AsMemory(0, read));
            }
        }
        await stream.FlushAsync();
        
        Console.WriteLine($"[FileServer] Отправлен: {info.FileName}");
        
        // Удаляем файл
        try
        {
            File.Delete(info.TempPath);
            _files.TryRemove(fileId, out _);
        }
        catch { }
        
        FileDownloaded?.Invoke(this, new FileDownloadedEventArgs { FileId = fileId });
    }
    
    public void Cleanup(TimeSpan maxAge)
    {
        var cutoff = DateTime.UtcNow - maxAge;
        foreach (var kvp in _files.Where(x => x.Value.UploadedAt < cutoff).ToList())
        {
            try
            {
                File.Delete(kvp.Value.TempPath);
                _files.TryRemove(kvp.Key, out _);
            }
            catch { }
        }
    }
    
    public class FileInfo
    {
        public string FileId { get; set; } = "";
        public long SenderId { get; set; }
        public long TargetUserId { get; set; }
        public long ChatId { get; set; }
        public string FileName { get; set; } = "";
        public long FileSize { get; set; }
        public string TempPath { get; set; } = "";
        public DateTime UploadedAt { get; set; }
    }
}

public class FileUploadedEventArgs : EventArgs
{
    public string FileId { get; set; } = "";
    public long SenderId { get; set; }
    public long TargetUserId { get; set; }
    public long ChatId { get; set; }
    public string FileName { get; set; } = "";
    public long FileSize { get; set; }
}

public class FileDownloadedEventArgs : EventArgs
{
    public string FileId { get; set; } = "";
}
