namespace Dip.Worker.Options;
public sealed class WorkerOptions{public const string SectionName="Worker";public int BatchSize{get;init;}=50;public int PollingIntervalSeconds{get;init;}=5;public int MaxAttempts{get;init;}=5;public int LockSeconds{get;init;}=60;}
