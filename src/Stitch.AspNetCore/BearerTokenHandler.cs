namespace Stitch.AspNetCore;

internal sealed class StaticBearerTokenHandler : DelegatingHandler
{
    private readonly string _token;

    public StaticBearerTokenHandler(string token) => _token = token;

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken ct)
    {
        request.Headers.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _token);
        return base.SendAsync(request, ct);
    }
}

internal sealed class DynamicBearerTokenHandler : DelegatingHandler
{
    private readonly IServiceProvider _sp;
    private readonly Func<IServiceProvider, CancellationToken, Task<string>> _tokenFactory;

    public DynamicBearerTokenHandler(
        IServiceProvider sp,
        Func<IServiceProvider, CancellationToken, Task<string>> tokenFactory)
    {
        _sp = sp;
        _tokenFactory = tokenFactory;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken ct)
    {
        var token = await _tokenFactory(_sp, ct);
        request.Headers.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        return await base.SendAsync(request, ct);
    }
}
