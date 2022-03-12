using Mono.Cecil;

namespace WebForms.Models;

public class Control
{
    private readonly Dictionary<string, CustomAttribute> _attributes = new();

    public Control(AssemblyInfo assembly, TypeDefinition type)
    {
        Assembly = assembly;
        Type = type;

        Scan(type);

        ChildrenAsProperties = true;// _attributes.TryGetValue("System.Web.UI.ParseChildrenAttribute", out var parseChildren) && (parseChildren.ConstructorArguments.FirstOrDefault().Value as bool? ?? false);
    }
    
    public bool ChildrenAsProperties { get; set; }

    public AssemblyInfo Assembly { get; }
    
    public TypeDefinition Type { get; }

    public string Namespace => Type.Namespace;

    public string Name => Type.Name;

    public Dictionary<string, ControlProperty> Properties { get; } = new(StringComparer.OrdinalIgnoreCase);

    private void Scan(TypeDefinition? type)
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

            foreach (var attribute in type.CustomAttributes)
            {
                var name = attribute.AttributeType.FullName;
                
                if (!_attributes.ContainsKey(name))
                {
                    _attributes.Add(name, attribute);
                }
            }

            type = type.BaseType?.Resolve();
        }
    }
}
