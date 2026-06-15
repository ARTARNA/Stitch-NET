namespace Stitch.Core;

public interface IStitchInterceptor
{
    ValueTask OnRequestAsync(HttpRequestMessage request, CancellationToken cancellationToken = default);

    ValueTask OnResponseAsync(
        HttpRequestMessage request,
        HttpResponseMessage response,
        CancellationToken cancellationToken = default);
}
