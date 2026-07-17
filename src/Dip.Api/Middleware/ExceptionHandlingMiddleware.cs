using System.Diagnostics;
using Dip.Shared.Contracts.Api;

namespace Dip.Api.Middleware;

public sealed class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(
        RequestDelegate next,
        ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (OperationCanceledException)
            when (context.RequestAborted.IsCancellationRequested)
        {
            _logger.LogInformation(
                "Request was cancelled. TraceId: {TraceId}",
                GetTraceId(context));
        }
        catch (KeyNotFoundException exception)
        {
            await WriteErrorAsync(
                context,
                StatusCodes.Status404NotFound,
                "not_found",
                exception.Message);
        }
        catch (ArgumentException exception)
        {
            await WriteErrorAsync(
                context,
                StatusCodes.Status400BadRequest,
                "validation_error",
                exception.Message);
        }
        catch (UnauthorizedAccessException exception)
        {
            await WriteErrorAsync(
                context,
                StatusCodes.Status403Forbidden,
                "forbidden",
                exception.Message);
        }
        catch (InvalidOperationException exception)
        {
            await WriteErrorAsync(
                context,
                StatusCodes.Status409Conflict,
                "conflict",
                exception.Message);
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "Unhandled exception. TraceId: {TraceId}",
                GetTraceId(context));

            await WriteErrorAsync(
                context,
                StatusCodes.Status500InternalServerError,
                "internal_error",
                "An unexpected error occurred.");
        }
    }

    private static async Task WriteErrorAsync(
        HttpContext context,
        int status,
        string type,
        string title)
    {
        if (context.Response.HasStarted)
        {
            return;
        }

        context.Response.Clear();
        context.Response.StatusCode = status;
        context.Response.ContentType = "application/json";

        var traceId = GetTraceId(context);
        context.Response.Headers["X-Trace-Id"] = traceId;

        await context.Response.WriteAsJsonAsync(
            new ApiError(type, title, status, traceId),
            cancellationToken: context.RequestAborted);
    }

    private static string GetTraceId(HttpContext context) =>
        Activity.Current?.TraceId.ToString() ?? context.TraceIdentifier;
}
