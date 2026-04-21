namespace Stitch.Core;

public interface IStitchSerializer
{
    ValueTask<T?> DeserializeAsync<T>(HttpContent content, CancellationToken ct);
    HttpContent Serialize<T>(T value);
}
