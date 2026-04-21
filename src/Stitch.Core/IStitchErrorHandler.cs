namespace Stitch.Core;

public interface IStitchErrorHandler
{
    ValueTask EnsureSuccessAsync(HttpResponseMessage response, CancellationToken ct);
}
