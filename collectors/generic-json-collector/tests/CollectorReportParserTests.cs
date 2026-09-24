// Purpose: Verifies Generic JSON report parsing boundaries, invalid input handling, and cancellation behavior.
using GenericJsonCollector.Parsing;

namespace GenericJsonCollector.Tests;

public sealed class CollectorReportParserTests
{
    private readonly CollectorReportParser _parser = new();

    [Fact]
    public async Task ParseAsync_RejectsFilesLargerThanTenMiB()
    {
        var path = CollectorReportTestData.NewTempPath();
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            await using (var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write))
                stream.SetLength(10L * 1024 * 1024 + 1);

            var exception = await Assert.ThrowsAsync<InvalidDataException>(
                () => _parser.ParseAsync(path, CancellationToken.None));

            Assert.Contains("10 MiB", exception.Message, StringComparison.Ordinal);
        }
        finally
        {
            CollectorReportTestData.Cleanup(path);
        }
    }

    [Fact]
    public async Task ParseAsync_RejectsEmptyFile()
    {
        var path = CollectorReportTestData.NewTempPath();
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            await File.WriteAllBytesAsync(path, Array.Empty<byte>());

            var exception = await Assert.ThrowsAsync<InvalidDataException>(
                () => _parser.ParseAsync(path, CancellationToken.None));

            Assert.Contains("empty", exception.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            CollectorReportTestData.Cleanup(path);
        }
    }

    [Fact]
    public async Task ParseAsync_RejectsInvalidJson()
    {
        var path = CollectorReportTestData.NewTempPath();
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            await File.WriteAllTextAsync(path, "{ not-json", CancellationToken.None);

            var exception = await Assert.ThrowsAsync<InvalidDataException>(
                () => _parser.ParseAsync(path, CancellationToken.None));

            Assert.Contains("invalid", exception.Message, StringComparison.OrdinalIgnoreCase);
            Assert.NotNull(exception.InnerException);
        }
        finally
        {
            CollectorReportTestData.Cleanup(path);
        }
    }

    [Fact]
    public async Task ParseAsync_ObservesPreCancelledToken()
    {
        var path = CollectorReportTestData.NewTempPath();
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            await File.WriteAllTextAsync(
                path,
                CollectorReportTestData.Serialize(CollectorReportTestData.CreateValid()),
                CancellationToken.None);

            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => _parser.ParseAsync(path, cancellation.Token));
        }
        finally
        {
            CollectorReportTestData.Cleanup(path);
        }
    }
}
