namespace Dip.Api.Middleware;

public static class RawBodyBufferingExtensions
{
    public static IApplicationBuilder UseGitHubWebhookBodyBuffering(
        this IApplicationBuilder app)
    {
        return app.UseWhen(
            context => context.Request.Path.StartsWithSegments(
                "/api/v1/webhooks/github"),
            branch => branch.Use(async (context, next) =>
            {
                context.Request.EnableBuffering();
                await next();
            }));
    }
}
