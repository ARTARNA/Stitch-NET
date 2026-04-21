namespace Stitch.Core;

[AttributeUsage(AttributeTargets.Parameter)]
public sealed class BodyAttribute : Attribute { }

[AttributeUsage(AttributeTargets.Parameter)]
public sealed class QueryAttribute : Attribute
{
    public QueryAttribute() { }

    public QueryAttribute(string name) => Name = name;

    public string? Name { get; }
}

[AttributeUsage(AttributeTargets.Parameter)]
public sealed class HeaderAttribute : Attribute
{
    public HeaderAttribute(string name) => Name = name;

    public string Name { get; }
}
