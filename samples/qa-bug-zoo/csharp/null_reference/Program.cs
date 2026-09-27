try
{
    string? value = null;
    Console.WriteLine(value!.Length);
}
catch (Exception ex)
{
    Console.Error.WriteLine(
        $"DIP-RUNTIME-EXCEPTION:{ex.GetType().FullName}:{ex.Message}");
    throw;
}
