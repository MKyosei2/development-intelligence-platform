// Purpose: Defines P ip el in eS ta tu s domain behavior and invariants for o pe ra ti on s.
namespace Dip.Domain.Enums; public enum PipelineStatus { Queued, Running, Succeeded, PartiallySucceeded, Failed, Cancelled, TimedOut, Retrying, Superseded }
