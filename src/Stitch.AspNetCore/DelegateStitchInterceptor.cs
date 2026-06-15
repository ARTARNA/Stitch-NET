using Stitch.Core;

namespace Stitch.AspNetCore;

internal sealed class DelegateStitchInterceptor : IStitchInterceptor
{
    private readonly Func<HttpRequestMessage, CancellationToken, ValueTask> _onRequest;
    private readonly Func<HttpRequestMessage, HttpResponseMessage, CancellationToken, ValueTask>? _onResponse;

    public DelegateStitchInterceptor(
        Func<HttpRequestMessage, CancellationToken, ValueTask> onRequest,
        Func<HttpRequestMessage, HttpResponseMessage, CancellationToken, ValueTask>? onResponse)
    {
        _onRequest = onRequest;
        _onResponse = onResponse;
    }

    public ValueTask OnRequestAsync(HttpRequestMessage request, CancellationToken cancellationToken = default) =>
        _onRequest(request, cancellationToken);

    public ValueTask OnResponseAsync(
        HttpRequestMessage request,
        HttpResponseMessage response,
        CancellationToken cancellationToken = default) =>
        _onResponse?.Invoke(request, response, cancellationToken) ?? ValueTask.CompletedTask;
}
