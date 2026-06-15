using Stitch.Core;
using Stitch.Tests.Contracts;

namespace Stitch.Tests.Contracts;

[StitchClient]
public interface IProductsApi
{
    [Get("/products/{id}")]
    Task<ProductDto> GetProductAsync(Guid id, CancellationToken ct = default);

    [Get("/products")]
    Task<PagedResult<ProductDto>> ListProductsAsync(
        [Query] int page,
        [Query("page_size")] int pageSize,
        CancellationToken ct = default);

    [Post("/products")]
    Task<ProductDto> CreateProductAsync([Body] CreateProductRequest request, CancellationToken ct = default);

    [Get("/products/{id}")]
    Task<StitchResult<ProductDto, ApiError>> GetProductResultAsync(Guid id, CancellationToken ct = default);

    [Get("/products")]
    Task<StitchResponse<PagedResult<ProductDto>>> ListProductsWithHeadersAsync(
        [Query] int page,
        CancellationToken ct = default);

    [Multipart]
    [Post("/upload")]
    Task<UploadResult> UploadAsync(
        [Multipart("file")] Stream file,
        [Multipart("description")] string description,
        CancellationToken ct = default);

    [Delete("/products/{id}")]
    Task DeleteProductAsync(Guid id, CancellationToken ct = default);
}

[StitchClient]
public interface IAuthApi
{
    [Get("/me")]
    Task<string> GetMeAsync(
        [Header("X-Tenant")] string tenant,
        CancellationToken ct = default);
}
