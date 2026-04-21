namespace Stitch.Core;

[AttributeUsage(AttributeTargets.Method)]
public abstract class HttpVerbAttribute : Attribute
{
    protected HttpVerbAttribute(string path) => Path = path;

    public string Path { get; }
}
