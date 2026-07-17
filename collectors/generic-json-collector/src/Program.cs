using GenericJsonCollector.Clients;
using GenericJsonCollector.Commands;
using GenericJsonCollector.Parsing;
using GenericJsonCollector.Validation;

if (args.Length < 2)
{
    PrintUsage();
    return 1;
}

using var cancellationSource = new CancellationTokenSource();
Console.CancelKeyPress += (_, eventArgs) =>
{
    eventArgs.Cancel = true;
    cancellationSource.Cancel();
};

try
{
    var parser = new CollectorReportParser();
    var validator = new CollectorReportValidator();

    return args[0].ToLowerInvariant() switch
    {
        "--validate" when args.Length == 2 =>
            await new ValidateCommand(parser, validator)
                .ExecuteAsync(args[1], cancellationSource.Token),

        "--submit" =>
            await ExecuteSubmitAsync(args, parser, validator, cancellationSource.Token),

        _ => InvalidArguments()
    };
}
catch (OperationCanceledException)
{
    Console.Error.WriteLine("Operation cancelled.");
    return 3;
}
catch (Exception exception)
{
    Console.Error.WriteLine(exception.Message);
    return 3;
}

static async Task<int> ExecuteSubmitAsync(
    string[] arguments,
    CollectorReportParser parser,
    CollectorReportValidator validator,
    CancellationToken cancellationToken)
{
    if (arguments.Length < 4)
        return InvalidArguments();

    var reportPath = arguments[1];
    string? apiUrl = null;
    string? apiKey = null;
    var timeoutSeconds = 30;

    for (var index = 2; index < arguments.Length; index++)
    {
        switch (arguments[index])
        {
            case "--api-url" when index + 1 < arguments.Length:
                apiUrl = arguments[++index];
                break;
            case "--api-key" when index + 1 < arguments.Length:
                apiKey = arguments[++index];
                break;
            case "--timeout-seconds" when index + 1 < arguments.Length
                && int.TryParse(arguments[index + 1], out var timeout)
                && timeout is >= 1 and <= 300:
                timeoutSeconds = timeout;
                index++;
                break;
            default:
                Console.Error.WriteLine($"Unknown or incomplete option: {arguments[index]}");
                return 1;
        }
    }

    if (!Uri.TryCreate(apiUrl, UriKind.Absolute, out var baseUri)
        || baseUri.Scheme is not ("http" or "https"))
    {
        Console.Error.WriteLine("--api-url must be an absolute HTTP(S) URL.");
        return 1;
    }

    using var httpClient = new HttpClient
    {
        BaseAddress = baseUri,
        Timeout = TimeSpan.FromSeconds(timeoutSeconds)
    };

    var command = new SubmitCommand(
        parser,
        validator,
        new DipApiClient(httpClient, apiKey));

    return await command.ExecuteAsync(reportPath, cancellationToken);
}

static int InvalidArguments()
{
    PrintUsage();
    return 1;
}

static void PrintUsage()
{
    Console.Error.WriteLine(
        """
        Usage:
          generic-json-collector --validate <report.json>
          generic-json-collector --submit <report.json> --api-url <url> [--api-key <key>] [--timeout-seconds <1-300>]

        Exit codes:
          0: Success
          1: Invalid arguments
          2: Validation failed
          3: File, parsing, cancellation, or unexpected error
          4: API submission failed
        """);
}
