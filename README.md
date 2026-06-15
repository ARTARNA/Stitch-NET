# Stitch

[![NuGet](https://img.shields.io/nuget/v/Stitch.Core.svg?label=Stitch.Core)](https://www.nuget.org/packages/Stitch.Core)
[![NuGet](https://img.shields.io/nuget/dt/Stitch.Core.svg?label=downloads)](https://www.nuget.org/packages/Stitch.Core)
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

## How is this different from Refit?

Every developer who finds this asks the same question. Here's the honest comparison:

| | **Stitch** | **Refit** |
|---|---|---|
| **When mistakes surface** | Compile time — route token mismatches, invalid bindings, missing verb attributes all fail the build | Runtime — wrong route names, bad parameter binding, missing attributes throw when you call the method |
| **Generated code** | Real `.g.cs` file in your project tree — readable, navigable, breakpointable | Emitted into memory by a source generator — you can't open it, step through it, or grep it |
| **Diagnostics** | Roslyn analyzer with 9 error codes and a code fix for route typos | Limited compile-time validation |
| **Error model** | `StitchHttpException` with status, headers, and body; or `StitchResult<T, E>` for typed failures | `ApiException` at runtime |
| **Response headers** | `Task<StitchResponse<T>>` returns body + headers (pagination cursors, rate limits) | Requires custom `ApiResponse<T>` wrapper or manual `HttpResponseMessage` |
| **File uploads** | `[Multipart]` attribute on `Stream` / `IFormFile` parameters | `[Multipart]` attribute (similar) |
| **Interceptors** | `IStitchInterceptor` pipeline — before/after hooks without writing a `DelegatingHandler` | `DelegatingHandler` only |
| **Maturity** | Early — focused on compile-time safety and debuggability | Battle-tested, large ecosystem, more auth/serialization plugins |

Refit is a solid choice if you want maximum ecosystem coverage today. Stitch is the better fit if you want compile-time guarantees, generated code you can actually read, and diagnostics that catch route typos before you deploy.

---

## Packages

All four packages are published on [NuGet](https://www.nuget.org/packages/Stitch.Core). Install with `dotnet add package`.

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
        foreach (var __interceptor in _opts.Interceptors)
            await __interceptor.OnRequestAsync(__req, ct);
        using var __res = await _http.SendAsync(__req, HttpCompletionOption.ResponseHeadersRead, ct);
        foreach (var __interceptor in _opts.Interceptors)
            await __interceptor.OnResponseAsync(__req, __res, ct);
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
| `[Multipart("field")]` on `Stream` / `IFormFile` | Multipart file part |
| `[Multipart("field")]` on simple type | Multipart form field |

`[Body]` on a POST is optional — if you have a complex type and there's no ambiguity, it gets inferred. `[Query]` and `[Header]` are always explicit. If a situation is genuinely ambiguous, you get a compiler error instead of silent wrong behavior.

---

## File uploads

Mark a method with `[Multipart]` and bind file parameters with `[Multipart("fieldName")]`:

```csharp
[Multipart]
[Post("/upload")]
Task<UploadResult> UploadAsync(
    [Multipart("file")] Stream file,
    [Multipart("description")] string description,
    CancellationToken ct = default);
```

`Stream` and `IFormFile` are sent as file parts. Simple types are sent as form fields. You cannot mix `[Body]` and `[Multipart]` on the same method — the analyzer catches that at compile time (ST008).

---

## Response headers

When you need headers back — pagination cursors, rate limit info, ETags — return `Task<StitchResponse<T>>` instead of `Task<T>`:

```csharp
[Get("/products")]
Task<StitchResponse<PagedResult<ProductDto>>> ListProductsAsync(
    [Query] int page,
    CancellationToken ct = default);
```

```csharp
var response = await productsApi.ListProductsAsync(page: 1);

var products = response.Value;
var total = response.Headers["X-Total-Count"].First();
var nextCursor = response.Headers.TryGetValue("X-Next-Cursor", out var cursors)
    ? cursors.First()
    : null;
```

`StitchResponse<T>` includes the deserialized body, the HTTP status code, and all response headers (including content headers like `Content-Type`).

---

## Request interceptors

Hook into every request without writing a `DelegatingHandler`:

```csharp
builder.Services.AddStitchClient<IProductsApi>(opts =>
{
    opts.UseInterceptor(
        onRequest: async (req, ct) =>
        {
            logger.LogDebug("→ {Method} {Url}", req.Method, req.RequestUri);
        },
        onResponse: async (req, res, ct) =>
        {
            logger.LogDebug("← {Status}", res.StatusCode);
        });
});
```

For reusable logic, implement `IStitchInterceptor` and register with `opts.UseInterceptor(myInterceptor)`.

Interceptors run inside the generated client — after the request is built (URL, body, headers) but before `SendAsync`, and again after the response arrives. Auth handlers still run at the `HttpClient` pipeline level.

---

## Diagnostics

This is the part I care about most. All of these fire at compile time, not at runtime:

```
ST001  Route token '{id}' has no matching parameter
ST002  Parameter 'productId' matches no route token — did you mean '{id}'?
ST003  [Body] on a GET or DELETE method
ST004  Multiple [Body] parameters on the same method
ST005  Return type must be Task, Task<T>, or ValueTask<T>
ST006  [StitchClient] can only be applied to interfaces
ST007  Method has no HTTP verb attribute
ST008  [Body] and [Multipart] on the same method
ST009  [Multipart] on a GET or DELETE method
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

The test suite (`tests/Stitch.Tests`) runs integration tests against a WireMock HTTP server — covering route binding, query params, JSON bodies, multipart uploads, auth headers, `StitchResult`, `StitchResponse`, interceptors, and error handling.

Requires .NET 8 SDK or later.

### Publishing to NuGet

Packages publish automatically when you push a version tag:

```bash
git tag v0.1.2
git push origin v0.1.2
```

CI builds, runs tests, packs all four packages, and pushes to [nuget.org](https://www.nuget.org/packages/Stitch.Core). 

---

## License

MIT
