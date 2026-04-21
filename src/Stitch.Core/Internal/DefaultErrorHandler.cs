namespace Stitch.Core.Internal;

internal sealed class DefaultErrorHandler : IStitchErrorHandler
{
    public static readonly DefaultErrorHandler Instance = new();

    public async ValueTask EnsureSuccessAsync(HttpResponseMessage response, CancellationToken ct)
    {
        if (response.IsSuccessStatusCode)
            return;

        var body = await response.Content.ReadAsStringAsync(ct);
        var headers = response.Headers
            .ToDictionary(h => h.Key, h => h.Value);

        throw new StitchHttpException(response.StatusCode, headers, body);
    }
}
