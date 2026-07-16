using GenericJsonCollector.Commands;

if (args.Length != 2 || args[0] is not "--validate")
{
    Console.Error.WriteLine("Usage: generic-json-collector --validate <report.json>");
    return 1;
}

try
{
    return await new ValidateCommand().ExecuteAsync(args[1], CancellationToken.None);
}
catch (Exception ex)
{
    Console.Error.WriteLine(ex.Message);
    return 3;
}
