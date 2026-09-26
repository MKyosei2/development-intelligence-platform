// Purpose: Verifies pytest discovery does not convert repositories without test modules into false-positive QA failures.
using Dip.Infrastructure.QualityAutomation;

namespace Dip.Infrastructure.Tests.QualityAutomation;

public sealed class PythonQaEngineTests
{
    [Fact]
    public void HasPytestTargets_SourceOnlyRepository_ReturnsFalse()
    {
        using var workspace = TemporaryDirectory.Create();
        File.WriteAllText(
            System.IO.Path.Combine(workspace.Path, "app.py"),
            "print('ok')\n");

        Assert.False(
            PythonQaEngine.HasPytestTargets(workspace.Path));
    }

    [Theory]
    [InlineData("test_app.py")]
    [InlineData("app_test.py")]
    public void HasPytestTargets_TestModule_ReturnsTrue(string fileName)
    {
        using var workspace = TemporaryDirectory.Create();
        File.WriteAllText(
            System.IO.Path.Combine(workspace.Path, fileName),
            "def test_ok():\n    assert True\n");

        Assert.True(
            PythonQaEngine.HasPytestTargets(workspace.Path));
    }

    [Theory]
    [InlineData(".venv")]
    [InlineData("venv")]
    [InlineData("__pycache__")]
    [InlineData(".dipqa")]
    [InlineData("site-packages")]
    public void HasPytestTargets_IgnoresGeneratedOrEnvironmentDirectories(string directoryName)
    {
        using var workspace = TemporaryDirectory.Create();
        var ignored = System.IO.Path.Combine(
            workspace.Path,
            directoryName);
        Directory.CreateDirectory(ignored);
        File.WriteAllText(
            System.IO.Path.Combine(ignored, "test_fake.py"),
            "def test_fake():\n    assert False\n");

        Assert.False(
            PythonQaEngine.HasPytestTargets(workspace.Path));
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        private TemporaryDirectory(string path)
        {
            Path = path;
        }

        public string Path { get; }

        public static TemporaryDirectory Create()
        {
            var path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                "dip-pythonqa-tests-" + Guid.NewGuid().ToString("N"));

            Directory.CreateDirectory(path);
            return new TemporaryDirectory(path);
        }

        public void Dispose()
        {
            try
            {
                Directory.Delete(Path, true);
            }
            catch
            {
                // Best-effort test cleanup only.
            }
        }
    }
}
