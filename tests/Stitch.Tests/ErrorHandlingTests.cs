using Stitch.Core;

namespace Stitch.Tests;

public sealed class ErrorHandlingTests : IntegrationTestBase
{
    [Fact]
    public async Task Non_success_status_throws_StitchHttpException()
    {
        var id = Guid.NewGuid();
        Server.Given(WireMock.RequestBuilders.Request.Create()
                .WithPath($"/products/{id}")
                .UsingGet())
            .RespondWith(WireMock.ResponseBuilders.Response.Create()
                .WithStatusCode(500)
                .WithHeader("Content-Type", "application/json")
                .WithBody("internal error"));

        var ex = await Assert.ThrowsAsync<StitchHttpException>(
            () => Products.GetProductAsync(id));

        Assert.Equal(System.Net.HttpStatusCode.InternalServerError, ex.StatusCode);
        Assert.Equal("internal error", ex.Body);
    }
}
