// Purpose: Defines the allowed P ip el in eS ta tu s states used by the DIP domain.
namespace Dip.Domain.Enums; public enum PipelineStatus { Queued, Running, Succeeded, PartiallySucceeded, Failed, Cancelled, TimedOut, Retrying, Superseded }
