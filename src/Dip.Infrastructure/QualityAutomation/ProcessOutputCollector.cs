// Purpose: Collects bounded stdout/stderr without allowing unbounded process output to exhaust memory.
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using Dip.Application.Abstractions.Observability;
using Microsoft.Extensions.Options;

namespace Dip.Infrastructure.QualityAutomation;

internal static class ProcessOutputCollector
{
    internal static async Task<(string Text, bool Truncated)> ReadCappedAsync(
            StreamReader reader,
            int max,
            CancellationToken ct)
    {
        var sb = new StringBuilder(Math.Min(max, 65536));
        var buffer = new char[8192];
        var truncated = false;
        while (true)
        {
            var n = await reader.ReadAsync(buffer.AsMemory(0, buffer.Length), ct);
            if (n <= 0)
                break;
            var remaining = max - sb.Length;
            if (remaining > 0)
                sb.Append(buffer, 0, Math.Min(remaining, n));
            if (n > remaining)
                truncated = true;
        }
        if (truncated)
            sb.Append("\n[DIP: output truncated by QA sandbox]\n");
        return (sb.ToString(), truncated);
    }
}
