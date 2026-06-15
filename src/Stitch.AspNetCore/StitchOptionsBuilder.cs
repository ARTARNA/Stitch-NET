using Stitch.Core;

namespace Stitch.AspNetCore;

public sealed class StitchOptionsBuilder
{
    private string? _baseAddress;
    private IStitchSerializer? _serializer;
    private IStitchErrorHandler? _errorHandler;
    private readonly List<IStitchInterceptor> _interceptors = [];

    internal List<Func<IServiceProvider, DelegatingHandler>> HandlerFactories { get; } = [];

    public string? BaseAddress
    {
        get => _baseAddress;
        set => _baseAddress = value;
    }

    public StitchOptionsBuilder UseBearer(string token)
    {
        HandlerFactories.Add(_ => new StaticBearerTokenHandler(token));
        return this;
    }

    public StitchOptionsBuilder UseBearer(Func<IServiceProvider, Task<string>> tokenFactory)
    {
        HandlerFactories.Add(sp => new DynamicBearerTokenHandler(
            sp, (provider, _) => tokenFactory(provider)));
        return this;
    }

    public StitchOptionsBuilder UseBearer(Func<IServiceProvider, CancellationToken, Task<string>> tokenFactory)
    {
        HandlerFactories.Add(sp => new DynamicBearerTokenHandler(sp, tokenFactory));
        return this;
    }

    public StitchOptionsBuilder UseApiKey(string headerName, string key)
    {
        HandlerFactories.Add(_ => new ApiKeyHandler(headerName, key));
        return this;
    }

    public StitchOptionsBuilder UseBasicAuth(string username, string password)
    {
        HandlerFactories.Add(_ => new BasicAuthHandler(username, password));
        return this;
    }

    public StitchOptionsBuilder OnError(Func<HttpResponseMessage, CancellationToken, ValueTask> handler)
    {
        _errorHandler = new DelegateErrorHandler(handler);
        return this;
    }

    public StitchOptionsBuilder OnError(Func<HttpResponseMessage, ValueTask> handler)
    {
        _errorHandler = new DelegateErrorHandler((res, _) => handler(res));
        return this;
    }

    public StitchOptionsBuilder WithSerializer(IStitchSerializer serializer)
    {
        _serializer = serializer;
        return this;
    }

    public StitchOptionsBuilder UseInterceptor(IStitchInterceptor interceptor)
    {
        _interceptors.Add(interceptor);
        return this;
    }

    public StitchOptionsBuilder UseInterceptor(
        Func<HttpRequestMessage, CancellationToken, ValueTask> onRequest,
        Func<HttpRequestMessage, HttpResponseMessage, CancellationToken, ValueTask>? onResponse = null)
    {
        _interceptors.Add(new DelegateStitchInterceptor(onRequest, onResponse));
        return this;
    }

    internal StitchOptions Build()
    {
        var opts = new StitchOptions();
        if (_serializer != null) opts.Serializer = _serializer;
        if (_errorHandler != null) opts.ErrorHandler = _errorHandler;
        if (_interceptors.Count > 0) opts.Interceptors = _interceptors.ToArray();
        return opts;
    }
}
