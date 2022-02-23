namespace WebForms.Models;

public enum TokenType
{
    None,

    StartDirective,
    EndDirective,

    Expression,
    EvalExpression,
    Statement,

    ElementNamespace,
    ElementName,

    TagOpen,
    TagOpenSlash,
    TagClose,
    TagSlashClose,

    DocType,
    Comment,
    Text,

    Attribute,
    AttributeValue
}