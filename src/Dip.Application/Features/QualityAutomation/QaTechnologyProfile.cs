// Purpose: Defines the QaTechnologyProfile application contract.
using Dip.Domain.Enums;
using Dip.Domain.ValueObjects;
namespace Dip.Application.Features.QualityAutomation;

public sealed record QaTechnologyProfile(string Workspace,IReadOnlyList<LanguageId> Languages,IReadOnlyList<string> BuildSystems,IReadOnlyList<string> Frameworks,IReadOnlyList<string> GameEngines,IReadOnlyList<string> Evidence);
