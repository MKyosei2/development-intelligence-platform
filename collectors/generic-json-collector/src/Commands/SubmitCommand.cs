using GenericJsonCollector.Clients;
using GenericJsonCollector.Parsing;
using GenericJsonCollector.Validation;

namespace GenericJsonCollector.Commands;

public sealed class SubmitCommand
{
    private readonly CollectorReportParser _parser;
    private readonly CollectorReportValidator _validator;
    private readonly DipApiClient _client;

    public SubmitCommand(
        CollectorReportParser parser,
        CollectorReportValidator validator,
        DipApiClient client)
    {
        _parser = parser;
        _validator = validator;
        _client = client;
    }

    public async Task<int> ExecuteAsync(
        string path,
        CancellationToken cancellationToken)
    {
        var report = await _parser.ParseAsync(path, cancellationToken);
        var errors = _validator.Validate(report);

        if (errors.Count > 0)
        {
            Console.Error.WriteLine(
                $"Collector report validation failed with {errors.Count} error(s):");

            foreach (var error in errors)
                Console.Error.WriteLine($"- {error}");

            return 2;
        }

        var result = await _client.SubmitAsync(report, cancellationToken);

        if (!result.IsSuccess)
        {
            Console.Error.WriteLine(
                $"Submission failed ({result.StatusCode}): {result.ErrorMessage}");
            return 4;
        }

        Console.WriteLine(
            result.WasDuplicate
                ? $"Report already processed. PipelineRunId={result.PipelineRunId}"
                : $"Report accepted. PipelineRunId={result.PipelineRunId}");

        Console.WriteLine(
            $"PipelineStatus={result.PipelineStatus}, QualityGate={result.QualityGateStatus}");

        return 0;
    }
}
