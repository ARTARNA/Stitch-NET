using Microsoft.Extensions.DependencyInjection;
using Stitch.Core;

namespace Stitch.AspNetCore;

public static class ServiceCollectionExtensions
{
    public static IHttpClientBuilder AddStitchClient<TInterface>(
        this IServiceCollection services,
        Action<StitchOptionsBuilder> configure)
        where TInterface : class
    {
        var builder = new StitchOptionsBuilder();
        configure(builder);

        var opts = builder.Build();
        var implType = FindImplementationType(typeof(TInterface));
        var clientName = typeof(TInterface).FullName!;

        var httpBuilder = services.AddHttpClient(clientName, client =>
        {
            if (builder.BaseAddress is not null)
                client.BaseAddress = new Uri(builder.BaseAddress);
        });

        foreach (var factory in builder.HandlerFactories)
        {
            // Capture factory in a local to avoid closure issues in the loop
            var capturedFactory = factory;
            httpBuilder.AddHttpMessageHandler(sp => capturedFactory(sp));
        }

        services.AddTransient<TInterface>(sp =>
        {
            var httpFactory = sp.GetRequiredService<IHttpClientFactory>();
            var http = httpFactory.CreateClient(clientName);
            return (TInterface)Activator.CreateInstance(implType, http, opts)!;
        });

        return httpBuilder;
    }

    private static Type FindImplementationType(Type interfaceType)
    {
        var expected = "Stitch" + (interfaceType.Name.Length > 1 && interfaceType.Name[0] == 'I'
            ? interfaceType.Name.Substring(1)
            : interfaceType.Name);

        var implType = AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(a =>
            {
                try { return a.GetTypes(); }
                catch { return []; }
            })
            .FirstOrDefault(t =>
                t.Name == expected
                && !t.IsAbstract
                && !t.IsInterface
                && interfaceType.IsAssignableFrom(t));

        if (implType is null)
            throw new InvalidOperationException(
                $"No Stitch implementation found for {interfaceType.Name}. " +
                $"Ensure Stitch.Generator is added as an <AnalyzerReference> to your project.");

        return implType;
    }
}
