# Stitch

[![NuGet](https://img.shields.io/nuget/v/Stitch.Core.svg?label=Stitch.Core)](https://www.nuget.org/packages/Stitch.Core)
[![NuGet](https://img.shields.io/nuget/dt/Stitch.Core.svg?label=downloads)](https://www.nuget.org/packages/Stitch.Core)
[![NuGet](https://img.shields.io/nuget/v/Stitch.AspNetCore.svg?label=Stitch.AspNetCore)](https://www.nuget.org/packages/Stitch.AspNetCore)

Stitch generates HTTP clients from C# interfaces. You define the shape, it writes the code.

I built this because I got tired of HTTP client libraries that fail at runtime on mistakes you could have caught at compile time, and generate code you can never actually step through. The generated class shows up in Solution Explorer, you can open it, set a breakpoint in it, grep it. That's it, that's the whole pitch.

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

## Why not Refit?

Refit is good. If you're already using it, you probably don't need this.

The thing that bugged me about Refit was that it finds your mistakes when the app runs, not when you build it. Typo a route token? Runtime. Forget a verb attribute? Runtime. Put `[Body]` on a DELETE? Runtime, probably in prod. Stitch makes all of those build errors instead. There's an analyzer with 9 diagnostics and a lightbulb fix for the most common one.

The generated code being a real file matters too. Refit generates into memory and you can't touch it. With Stitch you can open `StitchProductsApi.g.cs`, read exactly what it does on the wire, and put a breakpoint in it. Whether that's important to you is up to you, but I found it useful every single time something went wrong at the HTTP level.

The honest downside: this is newer and has less of everything. No Newtonsoft plugin, no OAuth2 helpers, nothing the Refit ecosystem took years to build. If you need those, use Refit.

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

That's all it is. No proxies, no reflection at call time. You can read it, step through it, and there's nothing hiding behind it.

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

`[Body]` on a POST is optional. If you have a complex type and there's no ambiguity, it gets inferred. `[Query]` and `[Header]` are always explicit. If a situation is genuinely ambiguous, you get a compiler error instead of silent wrong behavior.

---

## File uploads

Mark the method with `[Multipart]` and tag each parameter with `[Multipart("fieldName")]`:

```csharp
[Multipart]
[Post("/upload")]
Task<UploadResult> UploadAsync(
    [Multipart("file")] Stream file,
    [Multipart("description")] string description,
    CancellationToken ct = default);
```

`Stream` and `IFormFile` become file parts; simple types become form fields. Mixing `[Body]` and `[Multipart]` on the same method is a compile error (ST008).

Note: plain `Stream` parameters use the field name as the filename. If you need a real filename, use `IFormFile` instead.

---

## Response headers

If you need headers back alongside the body (pagination cursors, rate limit info, ETags), return `Task<StitchResponse<T>>`:

```csharp
[Get("/products")]
Task<StitchResponse<PagedResult<ProductDto>>> ListProductsAsync(
    [Query] int page,
    CancellationToken ct = default);
```

```csharp
var response = await productsApi.ListProductsAsync(page: 1);

var products = response.Value;
var nextCursor = response.Headers.TryGetValue("X-Next-Cursor", out var v) ? v.First() : null;
```

`StitchResponse<T>` carries the deserialized body, the status code, and all response headers including content headers.

---

## Interceptors

Hook into every request without writing a `DelegatingHandler`:

```csharp
builder.Services.AddStitchClient<IProductsApi>(opts =>
{
    opts.UseInterceptor(
        onRequest: (req, ct) =>
        {
            logger.LogDebug("→ {Method} {Url}", req.Method, req.RequestUri);
            return ValueTask.CompletedTask;
        },
        onResponse: (req, res, ct) =>
        {
            logger.LogDebug("← {Status}", res.StatusCode);
            return ValueTask.CompletedTask;
        });
});
```

For anything reusable, implement `IStitchInterceptor` and pass the instance in instead. They run inside the generated client after the request is built but before it goes out, and again when the response comes back. Auth handlers you've added still run at the `HttpClient` level — interceptors are one layer inside that.

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

Non-2xx responses throw `StitchHttpException` by default. It has the status code, the headers, and the raw body string already read, so you're not doing another `ReadAsStringAsync` in your catch block just to log what actually happened.

```csharp
catch (StitchHttpException ex)
{
    logger.LogError("HTTP {Status}: {Body}", ex.StatusCode, ex.Body);
}
```

If the API returns a structured error body, use `StitchResult<T, E>` to handle success and failure without throwing:

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

// Dynamic bearer, resolved per request
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

### Publishing to NuGet

Push a version tag and CI handles the rest:

```bash
git tag v0.1.2
git push origin v0.1.2
```

Requires the `NUGET_API_KEY` secret set in GitHub Actions.

---

## License

MIT
