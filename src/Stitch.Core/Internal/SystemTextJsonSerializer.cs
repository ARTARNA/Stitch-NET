using System.Text;
using System.Text.Json;

namespace Stitch.Core.Internal;

internal sealed class SystemTextJsonSerializer : IStitchSerializer
{
    public static readonly SystemTextJsonSerializer Instance = new();

    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    public async ValueTask<T?> DeserializeAsync<T>(HttpContent content, CancellationToken ct)
    {
        await using var stream = await content.ReadAsStreamAsync(ct);
        return await JsonSerializer.DeserializeAsync<T>(stream, Options, ct);
    }

    public HttpContent Serialize<T>(T value)
    {
        var json = JsonSerializer.Serialize(value, Options);
        return new StringContent(json, Encoding.UTF8, "application/json");
    }
}
