// Purpose: Defines QaAdapterSelection.
using Dip.Domain.Enums;
using Dip.Domain.ValueObjects;
namespace Dip.Application.Features.QualityAutomation;

public sealed record QaAdapterSelection(LanguageId Language, IQaAdapter Adapter);
