// Purpose: Defines Domain Exception domain behavior and invariants for this repository area.
namespace Dip.Domain.Exceptions; public sealed class DomainException : Exception { public DomainException(string message) : base(message) { } }
