// Purpose: Verifies QA execution planning rejects invalid commands before touching external dependencies.
using Dip.Application.Features.QualityAutomation;
using Dip.Domain.ValueObjects;
using FluentAssertions;

namespace Dip.Application.Tests.QualityAutomation;

public sealed class QaExecutionPlannerTests
{
    [Fact]
    public async Task CreateAsync_RejectsEmptyProjectIdBeforeResolvingWorkspace()
    {
        var sut = new QaExecutionPlanner(null!, null!, null!, null!, null!);
        var command = new QaExecutionCommand(
            Guid.Empty, "unit-test", new LanguageId("csharp"), ".", null, null, null);

        Func<Task> action = async () => await sut.CreateAsync(command, Guid.NewGuid(), CancellationToken.None);

        await action.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*ProjectId*");
    }
}
