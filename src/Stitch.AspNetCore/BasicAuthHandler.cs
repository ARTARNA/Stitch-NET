using System.Net.Http.Headers;
using System.Text;

namespace Stitch.AspNetCore;

internal sealed class BasicAuthHandler : DelegatingHandler
{
    private readonly AuthenticationHeaderValue _header;

    public BasicAuthHandler(string username, string password)
    {
        var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{username}:{password}"));
        _header = new AuthenticationHeaderValue("Basic", credentials);
    }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken ct)
    {
        request.Headers.Authorization = _header;
        return base.SendAsync(request, ct);
    }
}
