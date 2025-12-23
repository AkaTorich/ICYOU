using Newtonsoft.Json;
using System.Text;

namespace ICYOU.Core.Protocol;

public class Packet
{
    public PacketType Type { get; set; }
    public long SequenceId { get; set; }
    public long UserId { get; set; }
    public string? SessionToken { get; set; }
    public string Data { get; set; } = string.Empty;
    
    private static long _sequenceCounter = 0;
    
    public Packet() 
    {
        SequenceId = Interlocked.Increment(ref _sequenceCounter);
    }
    
    public Packet(PacketType type) : this()
    {
        Type = type;
    }
    
    public Packet(PacketType type, object data) : this(type)
    {
        SetData(data);
    }
    
    /// <summary>
    /// Создать ответный пакет с тем же SequenceId
    /// </summary>
    public Packet CreateResponse(PacketType type, object? data = null)
    {
        var response = new Packet { Type = type, SequenceId = this.SequenceId };
        if (data != null)
            response.SetData(data);
        return response;
    }
    
    public void SetData<T>(T data)
    {
        Data = JsonConvert.SerializeObject(data);
    }
    
    public T? GetData<T>()
    {
        if (string.IsNullOrEmpty(Data))
            return default;
        return JsonConvert.DeserializeObject<T>(Data);
    }
    
    public byte[] Serialize()
    {
        var json = JsonConvert.SerializeObject(this);
        var bytes = Encoding.UTF8.GetBytes(json);
        var result = new byte[bytes.Length + 4];
        
        // Длина пакета в начале (4 байта)
        BitConverter.GetBytes(bytes.Length).CopyTo(result, 0);
        bytes.CopyTo(result, 4);
        
        return result;
    }
    
    public static Packet? Deserialize(byte[] data)
    {
        try
        {
            if (data.Length < 4)
                return null;
                
            var length = BitConverter.ToInt32(data, 0);
            if (data.Length < length + 4)
                return null;
                
            var json = Encoding.UTF8.GetString(data, 4, length);
            return JsonConvert.DeserializeObject<Packet>(json);
        }
        catch
        {
            return null;
        }
    }
    
    public static Packet? DeserializeWithoutLength(byte[] data)
    {
        try
        {
            var json = Encoding.UTF8.GetString(data);
            return JsonConvert.DeserializeObject<Packet>(json);
        }
        catch
        {
            return null;
        }
    }
}

