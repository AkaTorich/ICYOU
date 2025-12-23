namespace ICYOU.SDK;

/// <summary>
/// Сервис для работы с файлами
/// </summary>
public interface IFileService
{
    /// <summary>
    /// Отправить файл в чат
    /// </summary>
    Task<FileTransfer> SendFileAsync(long chatId, string filePath);
    
    /// <summary>
    /// Отправить файл пользователю
    /// </summary>
    Task<FileTransfer> SendFileToUserAsync(long userId, string filePath);
    
    /// <summary>
    /// Принять файл
    /// </summary>
    Task AcceptFileAsync(long transferId, string savePath);
    
    /// <summary>
    /// Отклонить файл
    /// </summary>
    Task RejectFileAsync(long transferId);
    
    /// <summary>
    /// Получить список активных передач
    /// </summary>
    Task<IEnumerable<FileTransfer>> GetActiveTransfersAsync();
    
    /// <summary>
    /// Отменить передачу
    /// </summary>
    Task CancelTransferAsync(long transferId);
    
    /// <summary>
    /// Событие прогресса передачи
    /// </summary>
    event EventHandler<FileTransferProgressEventArgs>? TransferProgress;
}

public class FileTransferProgressEventArgs : EventArgs
{
    public long TransferId { get; set; }
    public long BytesTransferred { get; set; }
    public long TotalBytes { get; set; }
    public double Progress => TotalBytes > 0 ? (double)BytesTransferred / TotalBytes * 100 : 0;
}

