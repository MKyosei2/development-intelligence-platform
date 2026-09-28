// Purpose: Implements Raw Body Buffering responsibilities for this repository area.
namespace Dip.Api.Middleware;
public static class RawBodyBufferingExtensions
{
    const string PathValue = "/api/v1/webhooks/github"; const int Threshold = 64 * 1024; const long Max = 5L * 1024 * 1024;
    public static IApplicationBuilder UseGitHubWebhookBodyBuffering(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);
        return app.UseWhen(c => c.Request.Path.StartsWithSegments(PathValue, StringComparison.OrdinalIgnoreCase), branch => branch.Use(async (context, next) =>
        {
            context.Request.EnableBuffering(Threshold, Max);
            try
            {
                await next(context);
            }
            finally
            {
                if (context.Request.Body.CanSeek) context.Request.Body.Position = 0;
            }
        }));
    }
}
