using Stitch.Core;

namespace Stitch.AspNetCore;

internal sealed class DelegateErrorHandler : IStitchErrorHandler
{
    private readonly Func<HttpResponseMessage, CancellationToken, ValueTask> _handler;

    public DelegateErrorHandler(Func<HttpResponseMessage, CancellationToken, ValueTask> handler)
        => _handler = handler;

    public ValueTask EnsureSuccessAsync(HttpResponseMessage response, CancellationToken ct)
        => _handler(response, ct);
}
