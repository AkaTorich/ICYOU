using System.Net.Sockets;
using ICYOU.Core.Protocol;

namespace ICYOU.Mobile.Services;

public class FileTransferService
{
    private static FileTransferService? _instance;
    public static FileTransferService Instance => _instance ??= new FileTransferService();

    private string? _serverHost;
    private int _filePort = 7778;
    private readonly string _filesPath;

    public event EventHandler<double>? TransferProgress;
    public string? LastError { get; private set; }
    public string FilesPath => _filesPath;

    private FileTransferService()
    {
        // Используем AppDataDirectory для хранения файлов на мобильных устройствах
        _filesPath = Path.Combine(FileSystem.AppDataDirectory, "Files");
        Directory.CreateDirectory(_filesPath);
        DebugLog.Write($"[FileTransferService] Папка для файлов: {_filesPath}");
    }

    public void SetServer(string host, int filePort = 7778)
    {
        _serverHost = host;
        _filePort = filePort;
        DebugLog.Write($"[FileTransferService] Сервер установлен: {host}:{filePort}");
    }

    public async Task<bool> UploadFileAsync(string filePath, long targetUserId, long chatId)
    {
        if (string.IsNullOrEmpty(_serverHost))
        {
            LastError = "Сервер не настроен";
            DebugLog.Write("[FileTransferService] Сервер не настроен");
            return false;
        }

        if (!File.Exists(filePath))
        {
            LastError = "Файл не найден";
            DebugLog.Write($"[FileTransferService] Файл не найден: {filePath}");
            return false;
        }

        var fileInfo = new FileInfo(filePath);
        TcpClient? client = null;
        NetworkStream? stream = null;

        try
        {
            DebugLog.Write($"[FileTransferService] Начало загрузки {fileInfo.Name} ({fileInfo.Length} байт)");

            client = new TcpClient();
            client.SendTimeout = 120000;
            client.ReceiveTimeout = 120000;

            await client.ConnectAsync(_serverHost, _filePort);
            stream = client.GetStream();

            // Формируем заголовок
            using var headerMs = new MemoryStream();
            using (var bw = new BinaryWriter(headerMs, System.Text.Encoding.UTF8, leaveOpen: true))
            {
                bw.Write((byte)1);                          // команда upload
                bw.Write(App.CurrentUser!.Id);              // senderId
                bw.Write(targetUserId);                     // targetUserId
                bw.Write(chatId);                           // chatId

                var nameBytes = System.Text.Encoding.UTF8.GetBytes(fileInfo.Name);
                bw.Write(nameBytes.Length);
                bw.Write(nameBytes);
                bw.Write(fileInfo.Length);                  // размер файла
            }

            // Отправляем заголовок
            var header = headerMs.ToArray();
            await stream.WriteAsync(header);

            // Отправляем файл потоком
            using (var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                var buffer = new byte[8192];
                long totalSent = 0;
                int bytesRead;

                while ((bytesRead = await fs.ReadAsync(buffer)) > 0)
                {
                    await stream.WriteAsync(buffer.AsMemory(0, bytesRead));
                    totalSent += bytesRead;

                    var progress = (double)totalSent / fileInfo.Length * 100;
                    TransferProgress?.Invoke(this, progress);
                    DebugLog.Write($"[FileTransferService] Прогресс: {progress:F1}%");
                }
            }

            await stream.FlushAsync();

            // Читаем ответ
            var responseLenBytes = new byte[4];
            await ReadExactAsync(stream, responseLenBytes, 4);
            var responseLen = BitConverter.ToInt32(responseLenBytes);

            if (responseLen > 0 && responseLen < 1000)
            {
                var responseBytes = new byte[responseLen];
                await ReadExactAsync(stream, responseBytes, responseLen);
                var fileId = System.Text.Encoding.UTF8.GetString(responseBytes);
                DebugLog.Write($"[FileTransferService] Файл загружен успешно, fileId: {fileId}");
                return true;
            }

            LastError = "Неверный ответ сервера";
            DebugLog.Write("[FileTransferService] Неверный ответ сервера");
            return false;
        }
        catch (Exception ex)
        {
            LastError = ex.Message;
            DebugLog.Write($"[FileTransferService] Ошибка загрузки: {ex.Message}");
            return false;
        }
        finally
        {
            stream?.Dispose();
            client?.Dispose();
        }
    }

    public async Task<(string? fileName, byte[]? data)> DownloadFileAsync(string fileId)
    {
        if (string.IsNullOrEmpty(_serverHost))
        {
            DebugLog.Write("[FileTransferService] Сервер не настроен");
            return (null, null);
        }

        TcpClient? client = null;
        NetworkStream? stream = null;

        try
        {
            DebugLog.Write($"[FileTransferService] Начало скачивания файла {fileId}");

            client = new TcpClient();
            client.ReceiveTimeout = 120000;

            await client.ConnectAsync(_serverHost, _filePort);
            stream = client.GetStream();

            // Отправляем запрос
            using var ms = new MemoryStream();
            using (var bw = new BinaryWriter(ms, System.Text.Encoding.UTF8, leaveOpen: true))
            {
                bw.Write((byte)2);  // команда download
                var idBytes = System.Text.Encoding.UTF8.GetBytes(fileId);
                bw.Write(idBytes.Length);
                bw.Write(idBytes);
            }

            await stream.WriteAsync(ms.ToArray());
            await stream.FlushAsync();

            // Читаем ответ
            var successByte = new byte[1];
            await ReadExactAsync(stream, successByte, 1);

            if (successByte[0] == 0)
            {
                DebugLog.Write($"[FileTransferService] Файл {fileId} не найден на сервере");
                return (null, null);
            }

            // Читаем имя файла
            var nameLenBytes = new byte[4];
            await ReadExactAsync(stream, nameLenBytes, 4);
            var nameLen = BitConverter.ToInt32(nameLenBytes);

            var nameBytes = new byte[nameLen];
            await ReadExactAsync(stream, nameBytes, nameLen);
            var fileName = System.Text.Encoding.UTF8.GetString(nameBytes);

            // Читаем размер файла
            var sizeBytesArr = new byte[8];
            await ReadExactAsync(stream, sizeBytesArr, 8);
            var fileSize = BitConverter.ToInt64(sizeBytesArr);

            DebugLog.Write($"[FileTransferService] Скачивание {fileName} ({fileSize} байт)");

            // Читаем данные
            var data = new byte[fileSize];
            long received = 0;
            var buffer = new byte[8192];

            while (received < fileSize)
            {
                var toRead = (int)Math.Min(buffer.Length, fileSize - received);
                var read = await stream.ReadAsync(buffer.AsMemory(0, toRead));
                if (read == 0) break;

                Array.Copy(buffer, 0, data, received, read);
                received += read;

                var progress = (double)received / fileSize * 100;
                TransferProgress?.Invoke(this, progress);
            }

            DebugLog.Write($"[FileTransferService] Файл {fileName} скачан успешно");
            return (fileName, data);
        }
        catch (Exception ex)
        {
            DebugLog.Write($"[FileTransferService] Ошибка скачивания: {ex.Message}");
            return (null, null);
        }
        finally
        {
            stream?.Dispose();
            client?.Dispose();
        }
    }

    private async Task ReadExactAsync(NetworkStream stream, byte[] buffer, int count)
    {
        int offset = 0;
        while (offset < count)
        {
            int read = await stream.ReadAsync(buffer.AsMemory(offset, count - offset));
            if (read == 0) throw new EndOfStreamException();
            offset += read;
        }
    }

    public string SaveToAppData(string fileName, byte[] data)
    {
        // Уникальное имя с timestamp и GUID для избежания конфликтов
        var uniqueId = $"{DateTime.Now:yyyyMMdd_HHmmss}_{Guid.NewGuid().ToString("N")[..8]}";
        var savePath = Path.Combine(_filesPath, $"{uniqueId}_{fileName}");
        File.WriteAllBytes(savePath, data);
        DebugLog.Write($"[FileTransferService] Файл сохранён: {savePath}");
        return savePath;
    }

    public string GetFileType(string fileName)
    {
        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        return ext switch
        {
            ".jpg" or ".jpeg" or ".png" or ".gif" or ".webp" or ".bmp" => "image",
            ".mp4" or ".webm" or ".avi" or ".mkv" or ".mov" => "video",
            ".mp3" or ".wav" or ".ogg" or ".flac" => "audio",
            _ => "file"
        };
    }

    public (string? type, string? name, byte[]? data, string? savedPath) ParseFileMessage(string content)
    {
        try
        {
            var endIndex = content.LastIndexOf(']');
            if (endIndex < 0) return (null, null, null, null);

            string inner;
            char separator;

            // Новый формат с | : [FILE|имя|тип|путь|base64]
            if (content.StartsWith("[FILE|"))
            {
                inner = content.Substring(6, endIndex - 6);
                separator = '|';
            }
            // Старый формат с : : [FILE:имя:тип:base64]
            else if (content.StartsWith("[FILE:"))
            {
                inner = content.Substring(6, endIndex - 6);
                separator = ':';
            }
            else
            {
                return (null, null, null, null);
            }

            var parts = inner.Split(separator, 4);

            string fileName, fileType;
            string? base64 = null;
            string? savedPath = null;

            if (parts.Length >= 3)
            {
                fileName = parts[0];
                fileType = parts[1];

                if (parts.Length == 4)
                {
                    savedPath = parts[2];
                    base64 = parts[3];
                }
                else
                {
                    base64 = parts[2];
                }
            }
            else
            {
                return (null, null, null, null);
            }

            byte[]? data = null;

            // Сначала пробуем загрузить из локального файла
            if (!string.IsNullOrEmpty(savedPath) && File.Exists(savedPath))
            {
                data = File.ReadAllBytes(savedPath);
            }
            // Если файл не найден по пути - ищем в Files по имени
            else if (!string.IsNullOrEmpty(fileName))
            {
                var files = Directory.GetFiles(_filesPath, $"*_{fileName}");
                if (files.Length > 0)
                {
                    // Берём самый свежий
                    var latestFile = files.OrderByDescending(f => new FileInfo(f).CreationTime).First();
                    data = File.ReadAllBytes(latestFile);
                    savedPath = latestFile;
                }
            }

            // Если файл не найден локально - пробуем base64
            if (data == null && !string.IsNullOrEmpty(base64))
            {
                try
                {
                    data = Convert.FromBase64String(base64);
                }
                catch (Exception ex)
                {
                    DebugLog.Write($"[FileTransferService] Ошибка декодирования base64: {ex.Message}");
                }
            }

            return (fileType, fileName, data, savedPath);
        }
        catch (Exception ex)
        {
            DebugLog.Write($"[FileTransferService] Ошибка парсинга файлового сообщения: {ex.Message}");
            return (null, null, null, null);
        }
    }

    public string SaveFile(string fileName, byte[] data)
    {
        var savePath = Path.Combine(_filesPath, $"{DateTime.Now:yyyyMMdd_HHmmss}_{fileName}");
        File.WriteAllBytes(savePath, data);
        DebugLog.Write($"[FileTransferService] Файл сохранён: {savePath}");
        return savePath;
    }
}
