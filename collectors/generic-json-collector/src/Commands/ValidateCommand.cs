using GenericJsonCollector.Parsing;
using GenericJsonCollector.Validation;

namespace GenericJsonCollector.Commands;

public sealed class ValidateCommand
{
    private readonly CollectorReportParser _parser = new();
    private readonly CollectorReportValidator _validator = new();

    public async Task<int> ExecuteAsync(string path, CancellationToken cancellationToken)
    {
        var report = await _parser.ParseAsync(path, cancellationToken);
        var errors = _validator.Validate(report);

        if (errors.Count == 0)
        {
            Console.WriteLine("Collector report is valid.");
            return 0;
        }

        foreach (var error in errors)
            Console.Error.WriteLine(error);

        return 2;
    }
}
