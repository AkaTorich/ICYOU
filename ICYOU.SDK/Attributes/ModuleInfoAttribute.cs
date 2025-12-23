namespace ICYOU.SDK;

/// <summary>
/// Атрибут для указания информации о модуле
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public class ModuleInfoAttribute : Attribute
{
    public string Id { get; }
    public string Name { get; }
    public string Version { get; }
    public string Author { get; set; } = "Unknown";
    public string Description { get; set; } = string.Empty;
    
    public ModuleInfoAttribute(string id, string name, string version)
    {
        Id = id;
        Name = name;
        Version = version;
    }
}

