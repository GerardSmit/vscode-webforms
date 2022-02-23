using System.Collections;
using System.Resources;
using Mono.Cecil;

namespace WebForms.Models;

public sealed class AssemblyInfo : IDisposable
{
    public AssemblyInfo(AssemblyDefinition assembly)
    {
        Assembly = assembly;

        var name = assembly.Name.Name + ".resources";
        var module = assembly.MainModule;
        var resources = module.Resources.FirstOrDefault(i => i.Name == name);

        if (resources is EmbeddedResource embeddedResource)
        {
            using var reader = embeddedResource.GetResourceStream();
            var resourceReader = new ResourceReader(reader);

            foreach (DictionaryEntry entry in resourceReader)
            {
                var key = entry.Key.ToString();
                var value = entry.Value?.ToString();

                if (key != null && value != null)
                {
                    Resources[key] = value;
                }
            }
        }
    }

    public Dictionary<string, string> Resources { get; } = new();
    
    public AssemblyDefinition Assembly { get; }

    public bool HasControl { get; set; }

    public void Dispose()
    {
        Resources.Clear();
        Assembly.Dispose();
    }
}
