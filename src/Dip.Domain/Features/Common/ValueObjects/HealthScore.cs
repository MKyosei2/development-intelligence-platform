// Purpose: Defines H ea lt hS co re domain behavior and invariants for this repository area.
using System.Globalization;
using Dip.Domain.Exceptions;
namespace Dip.Domain.ValueObjects;
 public readonly record struct HealthScore { public decimal Value { get;
 }
public HealthScore(decimal value) { if (value is < 0 or > 100) throw new DomainException("Health score must be between 0 and 100.");
 Value = decimal.Round(value, 2);
 }
public override string ToString() => Value.ToString("0.00", CultureInfo.InvariantCulture);
 }
