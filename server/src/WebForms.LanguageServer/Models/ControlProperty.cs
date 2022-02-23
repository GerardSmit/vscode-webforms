using Mono.Cecil;

namespace WebForms.Models;

public class ControlProperty
{
    private readonly PropertyDefinition _property;

    public ControlProperty(Control control, PropertyDefinition property)
    {
        _property = property;
        Control = control;

        var assembly = control.Assembly;

        foreach (var attribute in property.CustomAttributes)
        {
            var value = attribute.ConstructorArguments.FirstOrDefault().Value?.ToString();

            if (value == null)
            {
                continue;
            }

            switch (attribute.AttributeType.Name)
            {
                case "WebSysDescriptionAttribute" when assembly.Resources.TryGetValue(value, out value):
                    Description = value;
                    break;
                case "DescriptionAttribute":
                    Description = value;
                    break;
                case "DefaultValueDescription":
                    DefaultValue = value;
                    break;
                case "WebCategoryAttribute" when assembly.Resources.TryGetValue("Category_" + value, out value):
                    WebCategory = value;
                    break;
                case "CategoryAttribute":
                    WebCategory = value;
                    break;
            }
        }
    }

    public string Name => _property.Name;

    public Control Control { get; }

    public TypeReference Type => _property.PropertyType;

    public string Description { get; } = "";

    public string DefaultValue { get; } = "";

    public string WebCategory { get; } = "";
}
