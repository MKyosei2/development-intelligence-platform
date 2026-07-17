using System.Diagnostics;
using System.Text.Json;

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
                "Request cancelled. TraceId={TraceId}",
                GetTraceId(context));

            if (!context.Response.HasStarted)
                context.Response.StatusCode = 499;
        }
        catch (BadHttpRequestException exception)
            when (exception.StatusCode == StatusCodes.Status413PayloadTooLarge)
        {
            await WriteProblemAsync(
                context,
                StatusCodes.Status413PayloadTooLarge,
                "payload_too_large",
                "Request payload is too large.",
                exception.Message);
        }
        catch (BadHttpRequestException exception)
        {
            await WriteProblemAsync(
                context,
                exception.StatusCode,
                "bad_request",
                "The request could not be read.",
                exception.Message);
        }
        catch (JsonException exception)
        {
            await WriteProblemAsync(
                context,
                StatusCodes.Status400BadRequest,
                "invalid_json",
                "The request contains invalid JSON.",
                exception.Message);
        }
        catch (KeyNotFoundException exception)
        {
            await WriteProblemAsync(
                context,
                StatusCodes.Status404NotFound,
                "not_found",
                exception.Message,
                exception.Message);
        }
        catch (ArgumentException exception)
        {
            await WriteProblemAsync(
                context,
                StatusCodes.Status400BadRequest,
                "validation_error",
                "Collector report validation failed.",
                exception.Message);
        }
        catch (UnauthorizedAccessException exception)
        {
            await WriteProblemAsync(
                context,
                StatusCodes.Status403Forbidden,
                "forbidden",
                "Access is forbidden.",
                exception.Message);
        }
        catch (InvalidOperationException exception)
        {
            await WriteProblemAsync(
                context,
                StatusCodes.Status409Conflict,
                "conflict",
                "The request conflicts with the current resource state.",
                exception.Message);
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "Unhandled exception. TraceId={TraceId}",
                GetTraceId(context));

            await WriteProblemAsync(
                context,
                StatusCodes.Status500InternalServerError,
                "internal_error",
                "An unexpected error occurred.",
                null);
        }
    }

    private static async Task WriteProblemAsync(
        HttpContext context,
        int status,
        string type,
        string title,
        string? detail)
    {
        if (context.Response.HasStarted)
            return;

        context.Response.Clear();
        context.Response.StatusCode = status;
        context.Response.ContentType = "application/problem+json";

        var traceId = GetTraceId(context);
        context.Response.Headers["X-Trace-Id"] = traceId;

        await context.Response.WriteAsJsonAsync(
            new
            {
                type,
                title,
                status,
                detail,
                traceId,
                instance = context.Request.Path.Value
            },
            cancellationToken: context.RequestAborted);
    }

    private static string GetTraceId(HttpContext context) =>
        Activity.Current?.TraceId.ToString() ?? context.TraceIdentifier;
}
