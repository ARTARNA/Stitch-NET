using Stitch.Core.Internal;

namespace Stitch.Core;

public sealed class StitchOptions
{
    public IStitchSerializer Serializer { get; set; } = SystemTextJsonSerializer.Instance;
    public IStitchErrorHandler ErrorHandler { get; set; } = DefaultErrorHandler.Instance;
}
