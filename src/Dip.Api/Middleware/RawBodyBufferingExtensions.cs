namespace Dip.Api.Middleware;

public static class RawBodyBufferingExtensions
{
    private const string GitHubWebhookPath = "/api/v1/webhooks/github";
    private const int MemoryBufferThresholdBytes = 64 * 1024;
    private const long MaximumBufferBytes = 5L * 1024 * 1024;

    public static IApplicationBuilder UseGitHubWebhookBodyBuffering(
        this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        return app.UseWhen(
            context => context.Request.Path.StartsWithSegments(
                GitHubWebhookPath,
                StringComparison.OrdinalIgnoreCase),
            branch => branch.Use(async (context, next) =>
            {
                context.Request.EnableBuffering(
                    MemoryBufferThresholdBytes,
                    MaximumBufferBytes);

                try
                {
                    await next(context);
                }
                finally
                {
                    if (context.Request.Body.CanSeek)
                    {
                        context.Request.Body.Position = 0;
                    }
                }
            }));
    }
}
