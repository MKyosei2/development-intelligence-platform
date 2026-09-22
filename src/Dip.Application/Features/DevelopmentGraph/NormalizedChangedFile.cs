// Purpose: Defines NormalizedChangedFile.
using Dip.Domain.Enums;
namespace Dip.Application.Features.DevelopmentGraph;

public sealed record NormalizedChangedFile(string Path, string Status, int Additions = 0, int Deletions = 0);
