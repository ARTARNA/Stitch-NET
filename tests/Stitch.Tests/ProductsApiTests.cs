using Stitch.Core;
using Stitch.Tests.Contracts;

namespace Stitch.Tests;

public sealed class ProductsApiTests : IntegrationTestBase
{
    [Fact]
    public async Task GetProduct_deserializes_json_response()
    {
        var id = Guid.Parse("11111111-1111-1111-1111-111111111111");
        StubGet($"/products/{id}", new { id, name = "Widget", price = 9.99m });

        var product = await Products.GetProductAsync(id);

        Assert.Equal(id, product.Id);
        Assert.Equal("Widget", product.Name);
        Assert.Equal(9.99m, product.Price);
    }

    [Fact]
    public async Task ListProducts_sends_query_parameters()
    {
        StubGet("/products", new
        {
            items = Array.Empty<ProductDto>(),
            total = 0
        });

        await Products.ListProductsAsync(page: 2, pageSize: 50);

        var request = Assert.Single(Server.LogEntries);
        var url = request.RequestMessage.Url ?? request.RequestMessage.Path;
        Assert.Contains("page=2", url);
        Assert.Contains("page_size=50", url);
    }

    [Fact]
    public async Task CreateProduct_sends_json_body()
    {
        var created = new { id = Guid.NewGuid(), name = "Gadget", price = 19.99m };
        StubPost("/products", created);

        var result = await Products.CreateProductAsync(new CreateProductRequest("Gadget", 19.99m));

        Assert.Equal("Gadget", result.Name);
        var entry = Assert.Single(Server.LogEntries);
        Assert.Equal("POST", entry.RequestMessage.Method);
        Assert.Contains("Gadget", entry.RequestMessage.Body);
    }

    [Fact]
    public async Task GetProductResult_returns_failure_without_throwing()
    {
        var id = Guid.NewGuid();
        Server.Given(WireMock.RequestBuilders.Request.Create()
                .WithPath($"/products/{id}")
                .UsingGet())
            .RespondWith(WireMock.ResponseBuilders.Response.Create()
                .WithStatusCode(404)
                .WithHeader("Content-Type", "application/json")
                .WithBodyAsJson(new { message = "Not found", code = "NOT_FOUND" }));

        var result = await Products.GetProductResultAsync(id);

        Assert.True(result.IsFailure);
        Assert.Equal("NOT_FOUND", result.Error!.Code);
    }

    [Fact]
    public async Task ListProductsWithHeaders_returns_response_headers()
    {
        StubGet("/products", new { items = Array.Empty<ProductDto>(), total = 42 }, headers: new Dictionary<string, string>
        {
            ["X-Total-Count"] = "42",
            ["X-Next-Cursor"] = "abc123"
        });

        var response = await Products.ListProductsWithHeadersAsync(page: 1);

        Assert.Equal(42, response.Value.Total);
        Assert.True(response.Headers.ContainsKey("X-Total-Count"));
        Assert.Equal("42", response.Headers["X-Total-Count"].First());
        Assert.Equal("abc123", response.Headers["X-Next-Cursor"].First());
    }

    [Fact]
    public async Task Upload_sends_multipart_form_data()
    {
        Server.Given(WireMock.RequestBuilders.Request.Create()
                .WithPath("/upload")
                .UsingPost())
            .RespondWith(WireMock.ResponseBuilders.Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBodyAsJson(new { fileName = "test.txt", size = 11 }));

        await using var stream = new MemoryStream("hello world"u8.ToArray());
        var result = await Products.UploadAsync(stream, "my file");

        Assert.Equal("test.txt", result.FileName);
        var entry = Assert.Single(Server.LogEntries);
        Assert.Contains("multipart/form-data", entry.RequestMessage.Headers!["Content-Type"].First());
        Assert.Contains("hello world", entry.RequestMessage.Body);
        Assert.Contains("description", entry.RequestMessage.Body);
        Assert.Contains("my file", entry.RequestMessage.Body);
    }

    [Fact]
    public async Task DeleteProduct_sends_delete_request()
    {
        var id = Guid.NewGuid();
        Server.Given(WireMock.RequestBuilders.Request.Create()
                .WithPath($"/products/{id}")
                .UsingDelete())
            .RespondWith(WireMock.ResponseBuilders.Response.Create().WithStatusCode(204));

        await Products.DeleteProductAsync(id);

        var entry = Assert.Single(Server.LogEntries);
        Assert.Equal("DELETE", entry.RequestMessage.Method);
    }
}
