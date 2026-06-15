namespace Stitch.Core;

[AttributeUsage(AttributeTargets.Method | AttributeTargets.Parameter)]
public sealed class MultipartAttribute : Attribute
{
    public MultipartAttribute() { }

    public MultipartAttribute(string name) => Name = name;

    public string? Name { get; }
}
