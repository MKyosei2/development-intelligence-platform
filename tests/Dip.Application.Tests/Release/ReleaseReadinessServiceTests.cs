// Purpose: Verifies ReleaseReadinessService rejects invalid requests and fails closed when current-revision evidence is unavailable.
using System.Reflection;
using Dip.Application.Abstractions.Persistence;
using Dip.Application.Abstractions.Time;
using Dip.Application.Features.Release;

namespace Dip.Application.Tests.Release;

public sealed class ReleaseReadinessServiceTests
{
    [Fact]
    public async Task EvaluateAsync_RejectsEmptyProjectId()
    {
        var service = CreateService();
        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.EvaluateAsync(new(Guid.Empty, new string('a', 40)), CancellationToken.None));
    }

    [Fact]
    public async Task EvaluateAsync_RejectsMissingCommitSha()
    {
        var service = CreateService();
        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.EvaluateAsync(new(Guid.NewGuid(), " "), CancellationToken.None));
    }

    [Fact]
    public async Task EvaluateAsync_FailsClosedWhenCurrentEvidenceIsUnavailable()
    {
        var service = CreateService();
        var result = await service.EvaluateAsync(
            new(Guid.NewGuid(), new string('a', 40)),
            CancellationToken.None);

        Assert.Equal("NoGo", result.Decision);
        Assert.NotEmpty(result.Blockers);
        Assert.Contains(result.Blockers, blocker =>
            blocker.Contains("evidence", StringComparison.OrdinalIgnoreCase)
            || blocker.Contains("QA", StringComparison.OrdinalIgnoreCase));
    }

    private static ReleaseReadinessService CreateService() => new(
        DefaultProxy<IIncidentRepository>.Create(),
        DefaultProxy<IDurableJobRepository>.Create(),
        DefaultProxy<IPerformanceBudgetRepository>.Create(),
        DefaultProxy<IProjectTruthRepository>.Create(),
        DefaultProxy<IEvidenceRepository>.Create(),
        DefaultProxy<IChangeRiskRepository>.Create(),
        DefaultProxy<IReleaseEvidenceRepository>.Create(),
        DefaultProxy<IReleaseReadinessRepository>.Create(),
        DefaultProxy<IDevelopmentGraphRepository>.Create(),
        DefaultProxy<IQaGovernanceRepository>.Create(),
        new FakeUnitOfWork(),
        new FakeClock(new DateTimeOffset(2026, 9, 20, 0, 0, 0, TimeSpan.Zero)));

    private sealed class FakeUnitOfWork : IUnitOfWork
    {
        public Task<int> SaveChangesAsync(CancellationToken ct) => Task.FromResult(1);
    }

    private sealed class FakeClock(DateTimeOffset utcNow) : ISystemClock
    {
        public DateTimeOffset UtcNow => utcNow;
    }

    private class DefaultProxy<T> : DispatchProxy where T : class
    {
        public static T Create() => DispatchProxy.Create<T, DefaultProxy<T>>();

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            ArgumentNullException.ThrowIfNull(targetMethod);
            return DefaultValue(targetMethod.ReturnType);
        }

        private static object? DefaultValue(Type type)
        {
            if (type == typeof(Task))
                return Task.CompletedTask;

            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Task<>))
            {
                var resultType = type.GetGenericArguments()[0];
                var value = DefaultResult(resultType);
                return typeof(Task)
                    .GetMethods(BindingFlags.Public | BindingFlags.Static)
                    .Single(method => method.Name == nameof(Task.FromResult) && method.IsGenericMethodDefinition)
                    .MakeGenericMethod(resultType)
                    .Invoke(null, [value]);
            }

            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(ValueTask<>))
            {
                var resultType = type.GetGenericArguments()[0];
                var value = DefaultResult(resultType);
                return Activator.CreateInstance(type, value);
            }

            return DefaultResult(type);
        }

        private static object? DefaultResult(Type type)
        {
            if (TryCreateEmptySequence(type, out var sequence))
                return sequence;
            return type.IsValueType ? Activator.CreateInstance(type) : null;
        }

        private static bool TryCreateEmptySequence(Type type, out object? sequence)
        {
            sequence = null;
            if (!type.IsGenericType)
                return false;

            var definition = type.GetGenericTypeDefinition();
            if (definition != typeof(IReadOnlyList<>)
                && definition != typeof(IReadOnlyCollection<>)
                && definition != typeof(IEnumerable<>)
                && definition != typeof(IList<>)
                && definition != typeof(ICollection<>))
            {
                return false;
            }

            sequence = Array.CreateInstance(type.GetGenericArguments()[0], 0);
            return true;
        }
    }
}
