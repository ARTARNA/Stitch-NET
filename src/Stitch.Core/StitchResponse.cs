using System.Net;

namespace Stitch.Core;

public sealed class StitchResponse<T>
{
    public T Value { get; init; } = default!;
    public HttpStatusCode StatusCode { get; init; }
    public IReadOnlyDictionary<string, IEnumerable<string>> Headers { get; init; }
        = new Dictionary<string, IEnumerable<string>>();

    public bool IsSuccessStatusCode =>
        (int)StatusCode >= 200 && (int)StatusCode <= 299;
}
