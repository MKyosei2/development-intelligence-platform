// Purpose: Defines domain behavior and state for this feature area.
namespace Dip.Domain.Enums; public enum PipelineStatus { Queued, Running, Succeeded, PartiallySucceeded, Failed, Cancelled, TimedOut, Retrying, Superseded }
