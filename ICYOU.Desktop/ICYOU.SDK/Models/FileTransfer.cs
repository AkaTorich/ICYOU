namespace ICYOU.SDK;

/// <summary>
/// Модель передачи файла
/// </summary>
public class FileTransfer
{
    public long Id { get; set; }
    public long SenderId { get; set; }
    public long ReceiverId { get; set; }
    public long? ChatId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public FileTransferStatus Status { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public long BytesTransferred { get; set; }
}

public enum FileTransferStatus
{
    Pending = 0,
    InProgress = 1,
    Completed = 2,
    Rejected = 3,
    Cancelled = 4,
    Failed = 5
}

