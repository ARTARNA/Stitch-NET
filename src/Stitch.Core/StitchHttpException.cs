using System.Net;

namespace Stitch.Core;

public sealed class StitchHttpException : Exception
{
    public StitchHttpException(
        HttpStatusCode statusCode,
        IReadOnlyDictionary<string, IEnumerable<string>> headers,
        string body)
        : base($"HTTP {(int)statusCode} {statusCode}: {body}")
    {
        StatusCode = statusCode;
        Headers = headers;
        Body = body;
    }

    public HttpStatusCode StatusCode { get; }
    public IReadOnlyDictionary<string, IEnumerable<string>> Headers { get; }
    public string Body { get; }
}
