using System.Collections.Concurrent;
using System.Globalization;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Dip.Api.Controllers;

[ApiController]
public sealed class MetricsController : ControllerBase
{
    private readonly ApiMetricsRegistry _metrics;

    public MetricsController(ApiMetricsRegistry metrics)
    {
        _metrics = metrics;
    }

    [AllowAnonymous]
    [HttpGet("/metrics")]
    [Produces("text/plain")]
    public ContentResult GetMetrics() => Content(
        _metrics.Render(),
        "text/plain; version=0.0.4; charset=utf-8",
        Encoding.UTF8);
}

public sealed class ApiMetricsRegistry
{
    private static readonly double[] DurationBuckets =
    [
        0.005,
        0.01,
        0.025,
        0.05,
        0.1,
        0.25,
        0.5,
        1.0,
        2.5,
        5.0,
        10.0
    ];

    private readonly ConcurrentDictionary<(string Method, int Status), long>
        _requestCounts = new();
    private readonly long[] _durationBucketCounts =
        new long[DurationBuckets.Length];
    private long _durationMicroseconds;
    private long _durationCount;

    public void Record(
        string method,
        int statusCode,
        TimeSpan duration)
    {
        var normalizedMethod = method.ToUpperInvariant();
        _requestCounts.AddOrUpdate(
            (normalizedMethod, statusCode),
            1,
            static (_, current) => current + 1);

        var seconds = duration.TotalSeconds;
        for (var index = 0; index < DurationBuckets.Length; index++)
        {
            if (seconds <= DurationBuckets[index])
            {
                Interlocked.Increment(ref _durationBucketCounts[index]);
            }
        }

        Interlocked.Increment(ref _durationCount);
        Interlocked.Add(
            ref _durationMicroseconds,
            checked((long)Math.Round(
                duration.TotalMilliseconds * 1000.0,
                MidpointRounding.AwayFromZero)));
    }

    public string Render()
    {
        var output = new StringBuilder();

        output.AppendLine(
            "# HELP dip_api_requests_total Total HTTP requests processed by DIP API.");
        output.AppendLine("# TYPE dip_api_requests_total counter");

        foreach (var item in _requestCounts
                     .OrderBy(item => item.Key.Method, StringComparer.Ordinal)
                     .ThenBy(item => item.Key.Status))
        {
            output.Append("dip_api_requests_total{method=\"")
                .Append(item.Key.Method)
                .Append("\",status=\"")
                .Append(item.Key.Status.ToString(CultureInfo.InvariantCulture))
                .Append("\"} ")
                .AppendLine(item.Value.ToString(CultureInfo.InvariantCulture));
        }

        output.AppendLine(
            "# HELP http_server_request_duration_seconds HTTP request duration observed by DIP API.");
        output.AppendLine(
            "# TYPE http_server_request_duration_seconds histogram");

        for (var index = 0; index < DurationBuckets.Length; index++)
        {
            output.Append("http_server_request_duration_seconds_bucket{le=\"")
                .Append(DurationBuckets[index].ToString("G17", CultureInfo.InvariantCulture))
                .Append("\"} ")
                .AppendLine(
                    Volatile.Read(ref _durationBucketCounts[index])
                        .ToString(CultureInfo.InvariantCulture));
        }

        var durationCount = Interlocked.Read(ref _durationCount);
        var durationSeconds =
            Interlocked.Read(ref _durationMicroseconds) / 1_000_000.0;

        output.Append("http_server_request_duration_seconds_bucket{le=\"+Inf\"} ")
            .AppendLine(durationCount.ToString(CultureInfo.InvariantCulture));
        output.Append("http_server_request_duration_seconds_sum ")
            .AppendLine(
                durationSeconds.ToString("G17", CultureInfo.InvariantCulture));
        output.Append("http_server_request_duration_seconds_count ")
            .AppendLine(durationCount.ToString(CultureInfo.InvariantCulture));
        output.AppendLine("dip_api_build_info{version=\"1.0\"} 1");
        return output.ToString();
    }
}
