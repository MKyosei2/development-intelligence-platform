// Purpose: Defines D om ai nE xc ep ti on domain behavior and invariants for e xc ep ti on s.
namespace Dip.Domain.Exceptions; public sealed class DomainException : Exception { public DomainException(string message) : base(message) { } }
