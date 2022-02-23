using Mono.Cecil;

namespace WebForms.Models;

public class Control
{
    private readonly TypeDefinition _type;

    public Control(AssemblyInfo assembly, TypeDefinition type)
    {
        Assembly = assembly;
        _type = type;

        AddProperties(type);
    }

    public AssemblyInfo Assembly { get; }

    public string Namespace => _type.Namespace;

    public string Name => _type.Name;

    public Dictionary<string, ControlProperty> Properties { get; } = new(StringComparer.OrdinalIgnoreCase);

    private void AddProperties(TypeDefinition? type)
    {
        while (type != null)
        {
            foreach (var property in type.Properties.Where(i => i.SetMethod != null))
            {
                if (!Properties.ContainsKey(property.Name))
                {
                    Properties.Add(property.Name, new ControlProperty(this, property));
                }
            }

            type = type.BaseType?.Resolve();
        }
    }
}
