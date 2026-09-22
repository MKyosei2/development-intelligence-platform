// Purpose: Defines the IQaAdapterRegistry application boundary.
using Dip.Domain.Enums;
using Dip.Domain.ValueObjects;
namespace Dip.Application.Features.QualityAutomation;

public interface IQaAdapterRegistry { IReadOnlyList<string> AdapterIds { get; } IReadOnlyList<QaAdapterSelection> Resolve(LanguageId requested, QaTechnologyProfile profile, string workspace); }
