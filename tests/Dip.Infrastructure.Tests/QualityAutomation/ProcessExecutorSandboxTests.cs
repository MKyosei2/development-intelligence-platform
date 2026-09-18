// Purpose: Verifies behavior and invariants covered by P ro ce ss Ex ec ut or Sa nd bo xT es ts.
using Dip.Infrastructure.QualityAutomation;
using FluentAssertions;
using Microsoft.Extensions.Options;

namespace Dip.Infrastructure.Tests.QualityAutomation;

public sealed class ProcessExecutorSandboxTests
{
    [Fact]
    public async Task QaSandbox_RejectsUntrustedAbsoluteExecutableOutsideWorkspace()
    {
        var root=Path.Combine(Path.GetTempPath(),"dip-sandbox-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);var fake=Path.Combine(Path.GetTempPath(),"not-a-trusted-dip-tool-"+Guid.NewGuid().ToString("N"));await File.WriteAllTextAsync(fake,"noop");
        try{var sut=new ProcessExecutor(Options.Create(new QaExecutionOptions{SandboxEnabled=true}));Func<Task> act=async()=>{await sut.RunQaAsync(fake,Array.Empty<string>(),root,null,TimeSpan.FromSeconds(1),Path.Combine(root,"out.txt"),Path.Combine(root,"err.txt"),CancellationToken.None);};await act.Should().ThrowAsync<InvalidOperationException>();}
        finally{try{File.Delete(fake);}catch{}try{Directory.Delete(root,true);}catch{}}
    }

    [Fact]
    public async Task WindowsQaSandbox_AppliesJobObjectCpuAndMemoryLimits()
    {
        if(!OperatingSystem.IsWindows())return;
        var root=Path.Combine(Path.GetTempPath(),"dip-sandbox-win-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
        try
        {
            var sut=new ProcessExecutor(Options.Create(new QaExecutionOptions{SandboxEnabled=true,SandboxCpuPercent=50,SandboxMemoryLimitMb=1024,SanitizeEnvironment=true}));
            var result=await sut.RunQaAsync("dotnet",new[]{"--version"},root,null,TimeSpan.FromSeconds(20),Path.Combine(root,"out.txt"),Path.Combine(root,"err.txt"),CancellationToken.None);
            result.ExitCode.Should().Be(0,"the trusted dotnet probe should run inside the Windows QA sandbox. stdout: {0}; stderr: {1}",result.StandardOutput,result.StandardError);
            result.Sandbox.Should().NotBeNull();
            result.Sandbox!.WorkspaceBoundaryEnforced.Should().BeTrue();
            result.Sandbox.EnvironmentSanitized.Should().BeTrue();
            result.Sandbox.CpuLimitApplied.Should().BeTrue();
            result.Sandbox.MemoryLimitApplied.Should().BeTrue();
        }
        finally{try{Directory.Delete(root,true);}catch{}}
    }
}
