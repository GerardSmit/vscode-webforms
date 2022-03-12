using Mono.Cecil;

namespace WebForms;

public static class TypeExtensions
{
    public static bool IsAssignableTo(this TypeReference typeReference, string typeName)
    {
        var current = typeReference.Resolve();

        while (current != null)
        {
            if (current.FullName == typeName)
            {
                return true;
            }

            current = current.BaseType.Resolve();
        }

        return false;
    }
}
