using System.Collections.Concurrent;
using System.Reflection;
using Mono.Cecil;

namespace WebForms.Models;

public class ProjectAssemblyResolver : BaseAssemblyResolver
{
    private readonly ConcurrentDictionary<string, AssemblyInfo> _assemblies = new(StringComparer.InvariantCulture);

    public IEnumerable<AssemblyInfo> Assemblies => _assemblies.Values;

    public override AssemblyDefinition Resolve(AssemblyNameReference name)
    {
        var assembly = ResolveInfo(name.Name)?.Assembly;
        
        if (assembly != null)
        {
            return assembly;
        }

        assembly = base.Resolve(name);
        RegisterAssembly(assembly);
        return assembly;
    }
    
    public AssemblyInfo? LoadAssembly(string path)
    {
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            var assemblyName = AssemblyName.GetAssemblyName(path);

            if (assemblyName.Name == null)
            {
                return null;
            }
        
            AssemblyInfo CreateAssembly(string _)
            {
                var assembly = AssemblyDefinition.ReadAssembly(path, new ReaderParameters
                {
                    AssemblyResolver = this
                });

                return new AssemblyInfo(assembly);
            }

            return _assemblies.GetOrAdd(assemblyName.Name, CreateAssembly);
        }
        catch
        {
            return null;
        }
    }
    
    private AssemblyInfo RegisterAssembly(AssemblyDefinition assembly)
    {
        var info = new AssemblyInfo(assembly);
        _assemblies[assembly.Name.Name] = info;
        return info;
    }

    public TypeDefinition? ResolveType(string typeName)
    {
        return _assemblies.Select(i => i.Value.Assembly.MainModule.GetType(typeName)).FirstOrDefault(i => i != null);
    }

    public TypeDefinition? ResolveType(TypeReference baseType)
    {
        return ResolveInfo(baseType.Scope)?.Assembly.MainModule.GetType(baseType.FullName);
    }

    public AssemblyInfo? ResolveInfo(string name)
    {
        return _assemblies.TryGetValue(name, out var info) ? info : null;
    }

    private AssemblyInfo? ResolveInfo(IMetadataScope scope)
    {
        if (scope is not ModuleDefinition module)
        {
            return ResolveInfo(scope.Name);
        }
        
        var assemblyName = module.Assembly.Name;

        if (_assemblies.TryGetValue(assemblyName.Name, out var assembly))
        {
            return assembly;
        }
            
        try
        {
            var definition = base.Resolve(assemblyName);
            return RegisterAssembly(definition);
        }
        catch
        {
            return null;
        }
    }

    public void UnloadAssemblies()
    {
        foreach (var key in _assemblies.Keys)
        {
            if (_assemblies.TryRemove(key, out var assemblyInfo))
            {
                assemblyInfo.Dispose();
            }
        }
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (disposing)
        {
            UnloadAssemblies();
        }
    }
}
