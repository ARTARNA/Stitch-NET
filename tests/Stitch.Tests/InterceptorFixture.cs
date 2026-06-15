namespace Stitch.Tests;

internal sealed class InterceptorFixture : IntegrationTestBase
{
    public InterceptorFixture(List<string> requests, List<int> responses)
        : base(opts => opts.UseInterceptor(
            (req, _) =>
            {
                requests.Add(req.Method.Method);
                return ValueTask.CompletedTask;
            },
            (_, res, _) =>
            {
                responses.Add((int)res.StatusCode);
                return ValueTask.CompletedTask;
            }))
    {
    }
}
