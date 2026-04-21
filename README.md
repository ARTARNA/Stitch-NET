# Stitch

[![NuGet](https://img.shields.io/nuget/v/Stitch.Core.svg?label=Stitch.Core)](https://www.nuget.org/packages/Stitch.Core)
[![NuGet](https://img.shields.io/nuget/v/Stitch.AspNetCore.svg?label=Stitch.AspNetCore)](https://www.nuget.org/packages/Stitch.AspNetCore)

Stitch generates HTTP clients from C# interfaces. You define the shape, it writes the code.

I built this because I got tired of HTTP client libraries that fail at runtime on mistakes you could have caught at compile time, and generate code you can never actually step through. The generated class shows up in Solution Explorer, you can navigate to it, you can set breakpoints in it. That was the main motivation.

```csharp
[StitchClient]
public interface IProductsApi
{
    [Get("/products/{id}")]
    Task<ProductDto> GetProductAsync(Guid id, CancellationToken ct = default);

    [Post("/products")]
    Task<ProductDto> CreateProductAsync([Body] CreateProductRequest request, CancellationToken ct = default);
}
```

---

## Packages

| Package | Target | Purpose |
|---|---|---|
| `Stitch.Core` | net8.0 | Attributes, base types, `IStitchSerializer`, `IStitchErrorHandler`. |
| `Stitch.Generator` | netstandard2.0 | Source generator. Analyzer reference, zero runtime footprint. |
| `Stitch.Analyzers` | netstandard2.0 | Roslyn diagnostics and code fixes. Analyzer reference. |
| `Stitch.AspNetCore` | net8.0 | `AddStitchClient<T>`, `HttpClientFactory` integration, auth handlers. |

Requires .NET 8 or later. The generator and analyzer target netstandard2.0 so Roslyn can load them regardless of your project's TFM.

---

## Getting started

**1. Add packages**

In the project that defines your interfaces:

```bash
dotnet add package Stitch.Core
dotnet add package Stitch.Generator
dotnet add package Stitch.Analyzers
```

Then open the `.csproj` and mark Generator and Analyzers as analyzer-only so they don't end up as runtime dependencies:

```xml
<PackageReference Include="Stitch.Generator" Version="*">
  <IncludeAssets>analyzers</IncludeAssets>
  <PrivateAssets>all</PrivateAssets>
</PackageReference>
<PackageReference Include="Stitch.Analyzers" Version="*">
  <IncludeAssets>analyzers</IncludeAssets>
  <PrivateAssets>all</PrivateAssets>
</PackageReference>
```

In your host project:

```bash
dotnet add package Stitch.AspNetCore
```

**2. Define an interface**

```csharp
using Stitch.Core;

[StitchClient]
public interface IProductsApi
{
    [Get("/products/{id}")]
    Task<ProductDto> GetProductAsync(Guid id, CancellationToken ct = default);

    [Get("/products")]
    Task<PagedResult<ProductDto>> ListProductsAsync(
        [Query] int page = 1,
        [Query] int pageSize = 20,
        CancellationToken ct = default);

    [Post("/products")]
    Task<ProductDto> CreateProductAsync([Body] CreateProductRequest request, CancellationToken ct = default);
}
```

**3. Register in `Program.cs`**

```csharp
builder.Services.AddStitchClient<IProductsApi>(opts =>
{
    opts.BaseAddress = "https://api.example.com";
    opts.UseBearer(async sp => await sp.GetRequiredService<ITokenService>().GetAsync());
});
```

**4. Inject and call**

```csharp
public class ProductService(IProductsApi api)
{
    public Task<ProductDto> GetAsync(Guid id) => api.GetProductAsync(id);
}
```

The `StitchProductsApi` implementation is generated at build time and registered automatically.

---

## What gets generated

The output lives under **Analyzers > Stitch.Generator** in Solution Explorer. For `IProductsApi` it looks like this:

```csharp
internal sealed class StitchProductsApi : IProductsApi
{
    private readonly HttpClient _http;
    private readonly StitchOptions _opts;

    public StitchProductsApi(HttpClient http, StitchOptions opts)
    {
        _http = http;
        _opts = opts;
    }

    public async Task<ProductDto> GetProductAsync(Guid id, CancellationToken ct = default)
    {
        var url = $"/products/{Uri.EscapeDataString(id.ToString()!)}";
        using var __req = new HttpRequestMessage(HttpMethod.Get, url);
        using var __res = await _http.SendAsync(__req, HttpCompletionOption.ResponseHeadersRead, ct);
        await _opts.ErrorHandler.EnsureSuccessAsync(__res, ct);
        return (await _opts.Serializer.DeserializeAsync<ProductDto>(__res.Content, ct))!;
    }

    // ...
}
```

Straightforward generated code, no runtime magic. You can read it, step through it, and understand exactly what's happening on the wire.

---

## Parameter binding

Stitch does the obvious thing by default:

| Situation | Binding |
|---|---|
| Parameter name matches a route token | Route |
| Simple type with no route match | Query string |
| Complex type on POST / PUT / PATCH | Request body |
| `CancellationToken` | Passed to `SendAsync` |
| `[Header("X-Tenant")]` | Request header |

`[Body]` on a POST is optional - if you have a complex type and there's no ambiguity, it gets inferred. `[Query]` and `[Header]` are always explicit. If a situation is genuinely ambiguous, you get a compiler error instead of silent wrong behavior.

---

## Diagnostics

This is the part I care about most. All of these fire at compile time, not at runtime:

```
ST001  Route token '{id}' has no matching parameter
ST002  Parameter 'productId' matches no route token -- did you mean '{id}'?
ST003  [Body] on a GET or DELETE method
ST004  Multiple [Body] parameters on the same method
ST005  Return type must be Task, Task<T>, or ValueTask<T>
ST006  [StitchClient] can only be applied to interfaces
ST007  Method has no HTTP verb attribute
```

ST002 ships with a code fix. Click the lightbulb and the parameter gets renamed to match the route token, all references included.

---

## Registration

```csharp
builder.Services.AddStitchClient<IProductsApi>(opts =>
{
    opts.BaseAddress = "https://api.example.com";
    opts.UseBearer(async sp =>
    {
        var tokens = sp.GetRequiredService<ITokenService>();
        return await tokens.GetAccessTokenAsync();
    });
    opts.OnError(async response =>
    {
        if (response.StatusCode == HttpStatusCode.Unauthorized)
            throw new UnauthorizedException();
        await response.EnsureSuccessAsync();
    });
});
```

`AddStitchClient` returns `IHttpClientBuilder`, so Polly attaches the same way it does on any named client:

```csharp
builder.Services
    .AddStitchClient<IProductsApi>(opts => { ... })
    .AddResilienceHandler("default", pipeline =>
    {
        pipeline.AddRetry(new HttpRetryStrategyOptions { MaxRetryAttempts = 3 });
        pipeline.AddCircuitBreaker(new HttpCircuitBreakerStrategyOptions());
    });
```

---

## Error handling

Non-2xx responses throw `StitchHttpException` by default. It has the status code, headers, and the raw body string so you don't need an extra `ReadAsStringAsync` call to log what went wrong.

```csharp
catch (StitchHttpException ex)
{
    logger.LogError("HTTP {Status}: {Body}", ex.StatusCode, ex.Body);
}
```

If the API returns a structured error body, use `StitchResult<T, E>` instead of throwing:

```csharp
[Get("/products/{id}")]
Task<StitchResult<ProductDto, ApiError>> GetProductAsync(Guid id, CancellationToken ct = default);
```

```csharp
var result = await productsApi.GetProductAsync(id);

result.Match(
    product => Display(product),
    error   => logger.LogError(error.Message));
```

---

## Serialization

System.Text.Json with `JsonSerializerDefaults.Web` is the default. To swap it out, implement `IStitchSerializer`:

```csharp
builder.Services.AddStitchClient<IProductsApi>(opts =>
{
    opts.WithSerializer(new NewtonsoftJsonStitchSerializer());
});
```

---

## Auth

```csharp
// Static bearer
opts.UseBearer("my-token");

// Dynamic bearer, called per request
opts.UseBearer(async sp => await sp.GetRequiredService<ITokenService>().GetAsync());

// API key header
opts.UseApiKey("X-Api-Key", "my-key");

// Basic auth
opts.UseBasicAuth("username", "password");
```

Custom auth is a `DelegatingHandler`. Nothing Stitch-specific to implement, just add it to the builder returned by `AddStitchClient`.

---

## Building from source

```bash
git clone https://github.com/ARTARNA/Stitch-NET
cd Stitch-NET
dotnet build
dotnet test
```

Requires .NET 8 SDK or later.

---

## License

MIT
