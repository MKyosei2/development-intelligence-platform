// Purpose: Defines Commit Sha domain behavior and invariants.
using Dip.Domain.Exceptions;
namespace Dip.Domain.ValueObjects;
 public readonly record struct CommitSha { public string Value { get;
 }
public CommitSha(string value) { value = value?.Trim() ?? string.Empty;
if (value.Length is < 7 or > 64 || !value.All(Uri.IsHexDigit)) throw new DomainException("Commit SHA must be 7-64 hexadecimal characters.");
 Value = value.ToLowerInvariant();
 }
public override string ToString() => Value;
 }
