static async Task DoWorkAsync() => await Task.Delay(10);
static async Task RunAsync()
{
    DoWorkAsync(); // CS4014: intentionally not awaited.
    await Task.Delay(1);
}
await RunAsync();
