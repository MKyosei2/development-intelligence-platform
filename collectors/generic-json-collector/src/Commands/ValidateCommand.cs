using GenericJsonCollector.Parsing;
using GenericJsonCollector.Validation;

namespace GenericJsonCollector.Commands;

public sealed class ValidateCommand
{
    private readonly CollectorReportParser _parser;
    private readonly CollectorReportValidator _validator;

    public ValidateCommand(
        CollectorReportParser parser,
        CollectorReportValidator validator)
    {
        _parser = parser;
        _validator = validator;
    }

    public async Task<int> ExecuteAsync(
        string path,
        CancellationToken cancellationToken)
    {
        var report = await _parser.ParseAsync(path, cancellationToken);
        var errors = _validator.Validate(report);

        if (errors.Count == 0)
        {
            Console.WriteLine("Collector report is valid.");
            return 0;
        }

        Console.Error.WriteLine(
            $"Collector report validation failed with {errors.Count} error(s):");

        foreach (var error in errors)
            Console.Error.WriteLine($"- {error}");

        return 2;
    }
}
