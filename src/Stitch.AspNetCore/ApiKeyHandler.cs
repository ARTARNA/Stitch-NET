namespace Stitch.AspNetCore;

internal sealed class ApiKeyHandler : DelegatingHandler
{
    private readonly string _headerName;
    private readonly string _key;

    public ApiKeyHandler(string headerName, string key)
    {
        _headerName = headerName;
        _key = key;
    }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken ct)
    {
        request.Headers.TryAddWithoutValidation(_headerName, _key);
        return base.SendAsync(request, ct);
    }
}
