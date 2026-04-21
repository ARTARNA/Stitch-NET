namespace Stitch.Core;

public sealed class GetAttribute : HttpVerbAttribute
{
    public GetAttribute(string path) : base(path) { }
}

public sealed class PostAttribute : HttpVerbAttribute
{
    public PostAttribute(string path) : base(path) { }
}

public sealed class PutAttribute : HttpVerbAttribute
{
    public PutAttribute(string path) : base(path) { }
}

public sealed class PatchAttribute : HttpVerbAttribute
{
    public PatchAttribute(string path) : base(path) { }
}

public sealed class DeleteAttribute : HttpVerbAttribute
{
    public DeleteAttribute(string path) : base(path) { }
}
