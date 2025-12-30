using ICYOU.Core.Protocol;
using ICYOU.SDK;

namespace ICYOU.Server;

public class FileTransferManager
{
    private readonly Dictionary<long, ActiveTransfer> _transfers = new();
    private long _nextTransferId = 1;
    private readonly object _lock = new();
    
    public ActiveTransfer CreateTransfer(long senderId, FileTransferRequestData request)
    {
        lock (_lock)
        {
            var transfer = new ActiveTransfer
            {
                Id = _nextTransferId++,
                SenderId = senderId,
                ReceiverId = request.TargetUserId ?? 0,
                ChatId = request.ChatId,
                FileName = request.FileName,
                FileSize = request.FileSize,
                Status = FileTransferStatus.Pending,
                StartedAt = DateTime.UtcNow
            };
            
            _transfers[transfer.Id] = transfer;
            return transfer;
        }
    }
    
    public ActiveTransfer? GetTransfer(long transferId)
    {
        lock (_lock)
        {
            return _transfers.TryGetValue(transferId, out var transfer) ? transfer : null;
        }
    }
    
    public void AcceptTransfer(long transferId, long receiverId)
    {
        lock (_lock)
        {
            if (_transfers.TryGetValue(transferId, out var transfer))
            {
                transfer.Status = FileTransferStatus.InProgress;
                transfer.ReceiverId = receiverId;
            }
        }
    }
    
    public void RejectTransfer(long transferId)
    {
        lock (_lock)
        {
            if (_transfers.TryGetValue(transferId, out var transfer))
            {
                transfer.Status = FileTransferStatus.Rejected;
            }
        }
    }
    
    public void AddChunk(long transferId, int chunkIndex, byte[] data)
    {
        lock (_lock)
        {
            if (_transfers.TryGetValue(transferId, out var transfer))
            {
                transfer.BytesTransferred += data.Length;
                transfer.Chunks[chunkIndex] = data;
            }
        }
    }
    
    public void CompleteTransfer(long transferId)
    {
        lock (_lock)
        {
            if (_transfers.TryGetValue(transferId, out var transfer))
            {
                transfer.Status = FileTransferStatus.Completed;
                transfer.CompletedAt = DateTime.UtcNow;
            }
        }
    }
    
    public void CancelTransfer(long transferId)
    {
        lock (_lock)
        {
            if (_transfers.TryGetValue(transferId, out var transfer))
            {
                transfer.Status = FileTransferStatus.Cancelled;
            }
        }
    }
    
    public void CleanupOldTransfers(TimeSpan maxAge)
    {
        lock (_lock)
        {
            var cutoff = DateTime.UtcNow - maxAge;
            var toRemove = _transfers
                .Where(kv => kv.Value.StartedAt < cutoff && 
                       kv.Value.Status != FileTransferStatus.InProgress)
                .Select(kv => kv.Key)
                .ToList();
                
            foreach (var id in toRemove)
            {
                _transfers.Remove(id);
            }
        }
    }
}

public class ActiveTransfer
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
    public Dictionary<int, byte[]> Chunks { get; set; } = new();
}

