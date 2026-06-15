using Microsoft.Extensions.DependencyInjection;
using Stitch.Tests.Contracts;

namespace Stitch.Tests;

public sealed class AuthAndInterceptorTests : IntegrationTestBase
{
    public AuthAndInterceptorTests() : base(opts => opts.UseBearer("secret-token")) { }

    [Fact]
    public async Task Bearer_token_is_sent_on_requests()
    {
        StubGet("/me", "ok");

        var auth = Services.GetRequiredService<IAuthApi>();
        await auth.GetMeAsync("tenant-1");

        var entry = Assert.Single(Server.LogEntries);
        Assert.Equal("Bearer secret-token", entry.RequestMessage.Headers!["Authorization"].First());
    }

    [Fact]
    public async Task Custom_header_is_sent()
    {
        StubGet("/me", "ok");

        await Auth.GetMeAsync("acme-corp");

        var entry = Assert.Single(Server.LogEntries);
        Assert.Equal("acme-corp", entry.RequestMessage.Headers!["X-Tenant"].First());
    }

    [Fact]
    public async Task Interceptor_runs_before_and_after_request()
    {
        var requests = new List<string>();
        var responses = new List<int>();

        using var fixture = new InterceptorFixture(requests, responses);

        fixture.StubGet("/products/00000000-0000-0000-0000-000000000001",
            new { id = "00000000-0000-0000-0000-000000000001", name = "x", price = 1m });

        await fixture.Products.GetProductAsync(Guid.Parse("00000000-0000-0000-0000-000000000001"));

        Assert.Equal(["GET"], requests);
        Assert.Equal([200], responses);
    }
}
