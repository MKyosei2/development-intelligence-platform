// Purpose: Defines Pipeline Status domain behavior and invariants.
namespace Dip.Domain.Enums; public enum PipelineStatus { Queued, Running, Succeeded, PartiallySucceeded, Failed, Cancelled, TimedOut, Retrying, Superseded }
