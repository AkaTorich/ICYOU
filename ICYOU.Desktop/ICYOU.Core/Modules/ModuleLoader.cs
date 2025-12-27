using ICYOU.SDK;
using System.Reflection;

namespace ICYOU.Core.Modules;

public class ModuleLoader
{
    private readonly Dictionary<string, LoadedModule> _modules = new();
    private readonly IModuleContext _context;
    private readonly string _modulesPath;
    
    public IReadOnlyDictionary<string, LoadedModule> LoadedModules => _modules;
    
    public event EventHandler<IModule>? ModuleLoaded;
    public event EventHandler<IModule>? ModuleUnloaded;
    
    public ModuleLoader(IModuleContext context, string modulesPath)
    {
        _context = context;
        _modulesPath = modulesPath;
        
        if (!Directory.Exists(_modulesPath))
            Directory.CreateDirectory(_modulesPath);
    }
    
    public void LoadAllModules()
    {
        var dllFiles = Directory.GetFiles(_modulesPath, "*.dll");
        foreach (var dll in dllFiles)
        {
            try
            {
                LoadModule(dll);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to load module {dll}: {ex.Message}");
            }
        }
    }
    
    public IModule? LoadModule(string dllPath)
    {
        var assembly = Assembly.LoadFrom(dllPath);
        var moduleTypes = assembly.GetTypes()
            .Where(t => typeof(IModule).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract)
            .ToList();
            
        if (moduleTypes.Count == 0)
            throw new Exception("No IModule implementation found in assembly");
            
        var moduleType = moduleTypes.First();
        var module = (IModule)Activator.CreateInstance(moduleType)!;
        
        if (_modules.ContainsKey(module.Id))
            throw new Exception($"Module with ID '{module.Id}' is already loaded");
        
        module.Initialize(_context);
        
        _modules[module.Id] = new LoadedModule
        {
            Module = module,
            Assembly = assembly,
            FilePath = dllPath
        };
        
        ModuleLoaded?.Invoke(this, module);
        Console.WriteLine($"Loaded module: {module.Name} v{module.Version} by {module.Author}");
        
        return module;
    }
    
    public void UnloadModule(string moduleId)
    {
        if (!_modules.TryGetValue(moduleId, out var loaded))
            return;
            
        loaded.Module.Shutdown();
        _modules.Remove(moduleId);
        
        ModuleUnloaded?.Invoke(this, loaded.Module);
        Console.WriteLine($"Unloaded module: {loaded.Module.Name}");
    }
    
    public void UnloadAllModules()
    {
        foreach (var moduleId in _modules.Keys.ToList())
        {
            UnloadModule(moduleId);
        }
    }
    
    public IModule? GetModule(string moduleId)
    {
        return _modules.TryGetValue(moduleId, out var loaded) ? loaded.Module : null;
    }
}

public class LoadedModule
{
    public IModule Module { get; set; } = null!;
    public Assembly Assembly { get; set; } = null!;
    public string FilePath { get; set; } = string.Empty;
}

