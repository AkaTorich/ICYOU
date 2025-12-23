using System.Net.Sockets;
using ICYOU.Core.Protocol;

namespace ICYOU.Mobile.Services;

public class FileTransferService
{
    private static FileTransferService? _instance;
    public static FileTransferService Instance => _instance ??= new FileTransferService();

    private string? _serverHost;
    private int _serverPort;

    private FileTransferService()
    {
    }

    public void SetServer(string host, int port)
    {
        _serverHost = host;
        _serverPort = port;
        DebugLog.Write($"[FileTransferService] Сервер установлен: {host}:{port}");
    }

    public async Task<string?> UploadFileAsync(string filePath, long? chatId = null, long? targetUserId = null)
    {
        if (string.IsNullOrEmpty(_serverHost))
        {
            DebugLog.Write("[FileTransferService] Сервер не настроен");
            return null;
        }

        try
        {
            var fileName = Path.GetFileName(filePath);
            var fileSize = new FileInfo(filePath).Length;

            DebugLog.Write($"[FileTransferService] Загрузка файла {fileName} ({fileSize} байт)");

            // В упрощённой версии просто сохраняем локально
            // TODO: Реальная загрузка на сервер
            return fileName;
        }
        catch (Exception ex)
        {
            DebugLog.Write($"[FileTransferService] Ошибка загрузки: {ex.Message}");
            return null;
        }
    }

    public async Task<bool> DownloadFileAsync(string fileId, string savePath)
    {
        if (string.IsNullOrEmpty(_serverHost))
        {
            DebugLog.Write("[FileTransferService] Сервер не настроен");
            return false;
        }

        try
        {
            DebugLog.Write($"[FileTransferService] Загрузка файла {fileId}");

            // TODO: Реальная загрузка с сервера
            return true;
        }
        catch (Exception ex)
        {
            DebugLog.Write($"[FileTransferService] Ошибка скачивания: {ex.Message}");
            return false;
        }
    }
}
