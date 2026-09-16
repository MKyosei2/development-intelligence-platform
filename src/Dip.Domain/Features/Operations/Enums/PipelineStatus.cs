// Purpose: Defines P ip el in eS ta tu s states used by Operations.
namespace Dip.Domain.Enums; public enum PipelineStatus { Queued, Running, Succeeded, PartiallySucceeded, Failed, Cancelled, TimedOut, Retrying, Superseded }
