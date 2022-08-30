using System.Xml.Linq;
using Mono.Cecil;

namespace WebForms.Models;

public record ControlRegistration(string? Prefix, string? TagName, string? Source, string? Namespace, string? Assembly);

public sealed class Project : IDisposable
{
    public Project(string path)
    {
        Path = path;
        Resolver = new ProjectAssemblyResolver();
    }

    public string Path { get; }

    public ProjectAssemblyResolver Resolver { get; }

    public Dictionary<string, List<Control>> NamespaceControls { get; private set; } = new(StringComparer.OrdinalIgnoreCase);

    public List<string> Namespaces { get; set; } = new();

    public List<ControlRegistration> Registrations { get; } = new();

    public List<Document> Documents { get; } = new();

    public void Load()
    {
        var webConfigPath = System.IO.Path.Combine(Path, "web.config");

        if (File.Exists(webConfigPath))
        {
            LoadWebConfig(webConfigPath);
        }
    }

    private void LoadWebConfig(string webConfigPath)
    {
        var document = XDocument.Load(webConfigPath);
        var pages = document.Root?.Element("system.web")?.Element("pages");

        if (pages?.Element("controls") is { } pageControls)
        {
            foreach (var element in pageControls.Elements("add"))
            {
                if (element.Attribute("assembly")?.Value is { } assembly)
                {
                    assembly = AssemblyNameReference.Parse(assembly).Name;
                }
                else
                {
                    assembly = null;
                }

                Registrations.Add(new ControlRegistration(
                    element.Attribute("tagPrefix")?.Value,
                    element.Attribute("tagName")?.Value,
                    element.Attribute("src")?.Value,
                    element.Attribute("namespace")?.Value,
                    assembly
                ));
            }
        }

        if (pages?.Element("namespaces") is { } namespaces)
        {
            foreach (var element in namespaces.Elements("add"))
            {
                if (element.Attribute("namespace")?.Value is { } ns)
                {
                    Namespaces.Add(ns);
                }
            }
        }
    }

    public void LoadAssemblies()
    {
        foreach (var path in Directory.GetFiles(System.IO.Path.Combine(Path, "bin"), "*.dll"))
        {
            Resolver.LoadAssembly(path);
        }

        // TODO: Fix hard-coded .NET Framework path
        var defaultAssemblies = new[]
        {
            @"C:\Windows\Microsoft.NET\Framework\v4.0.30319\System.Web.dll",
            @"C:\Windows\Microsoft.NET\Framework\v4.0.30319\System.Web.Extensions.dll"
        };
        
        foreach (var path in defaultAssemblies)
        {
            Resolver.LoadAssembly(path);
        }
        
        LoadControls();
        UpdateDocuments();
    }

    public void UnloadAssemblies()
    {
        Resolver.UnloadAssemblies();
        UpdateDocuments();
    }

    private void UpdateDocuments()
    {
        foreach (var document in Documents)
        {
            document.IsProjectDirty = true;
        }

        foreach (var document in Documents)
        {
            document.UpdateProject();
        }
    }

    private void LoadControls()
    {
        var controls = new List<Control>();

        Parallel.ForEach(
            Resolver.Assemblies.SelectMany(a => a.Assembly.MainModule.GetTypes().Select(t => new {Type = t, Assembly = a})),
            info =>
            {
                var baseType = info.Type.BaseType;

                while (baseType != null)
                {
                    if (baseType.FullName == "System.Web.UI.Control")
                    {
                        info.Assembly.HasControl = true;

                        lock (controls)
                        {
                            controls.Add(new Control(info.Assembly, info.Type));
                        }
                    }

                    baseType = Resolver.ResolveType(baseType)?.BaseType;
                }
            }
        );

        NamespaceControls = controls
            .GroupBy(i => i.Namespace, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);
    }

    public void Dispose()
    {
        Resolver.Dispose();
    }
}
