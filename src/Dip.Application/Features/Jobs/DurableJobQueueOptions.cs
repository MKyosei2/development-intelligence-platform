// Purpose: Defines DurableJobQueueOptions.
using System.Text.Json;
using Dip.Application.Abstractions.Persistence;
using Dip.Application.Abstractions.Time;
using Dip.Domain.Entities;
using Dip.Domain.Enums;

namespace Dip.Application.Features.Jobs;

public sealed class DurableJobQueueOptions
{
    public const string SectionName="DurableJobs";
    public string StreamName{get;init;}="dip:jobs";
    public string DeadLetterStreamName{get;init;}="dip:jobs:dead-letter";
    public string ConsumerGroup{get;init;}="dip-job-workers";
    public int LeaseSeconds{get;init;}=60;
    public int HeartbeatSeconds{get;init;}=15;
    public int BlockMilliseconds{get;init;}=5000;
    public int RecoveryIntervalSeconds{get;init;}=15;
}
