using Microsoft.Extensions.DependencyInjection;
using Stitch.AspNetCore;
using Stitch.Tests.Contracts;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;

namespace Stitch.Tests;

public class IntegrationTestBase : IDisposable
{
    protected WireMockServer Server { get; }
    protected IServiceProvider Services { get; }

    protected IntegrationTestBase(Action<StitchOptionsBuilder>? configure = null)
    {
        Server = WireMockServer.Start();
        var services = new ServiceCollection();

        services.AddStitchClient<IProductsApi>(opts =>
        {
            opts.BaseAddress = Server.Url!;
            configure?.Invoke(opts);
        });

        services.AddStitchClient<IAuthApi>(opts =>
        {
            opts.BaseAddress = Server.Url!;
            configure?.Invoke(opts);
        });

        Services = services.BuildServiceProvider();
    }

    internal IProductsApi Products => Services.GetRequiredService<IProductsApi>();
    internal IAuthApi Auth => Services.GetRequiredService<IAuthApi>();

    internal void StubGet(string path, object body, int status = 200, IDictionary<string, string>? headers = null)
    {
        var response = Response.Create()
            .WithStatusCode(status)
            .WithHeader("Content-Type", "application/json")
            .WithBodyAsJson(body);

        if (headers != null)
        {
            foreach (var (key, value) in headers)
                response = response.WithHeader(key, value);
        }

        Server.Given(Request.Create().WithPath(path).UsingGet())
            .RespondWith(response);
    }

    internal void StubPost(string path, object? responseBody = null, int status = 200)
    {
        var response = Response.Create().WithStatusCode(status);
        if (responseBody != null)
            response = response.WithHeader("Content-Type", "application/json").WithBodyAsJson(responseBody);

        Server.Given(Request.Create().WithPath(path).UsingPost())
            .RespondWith(response);
    }

    public void Dispose() => Server.Dispose();
}
