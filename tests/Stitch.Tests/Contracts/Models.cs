namespace Stitch.Tests.Contracts;

public sealed record ProductDto(Guid Id, string Name, decimal Price);

public sealed record CreateProductRequest(string Name, decimal Price);

public sealed record ApiError(string Message, string Code);

public sealed record UploadResult(string FileName, long Size);

public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Total);
