using System.Diagnostics;
using System.Text;
using Dip.Application.Features.QualityAutomation;
using Dip.Domain.Enums;
using Dip.Domain.ValueObjects;

namespace Dip.Infrastructure.QualityAutomation;

/// <summary>
/// Verifies a proposed patch in an isolated temporary worktree/copy. The source
/// workspace is never modified. A proposal is "Verified" only when the patch
/// applies, regression tests are materialized safely, QA executes, every
/// available validation tool succeeds, and no Critical finding remains.
/// </summary>
public sealed class VerifiedFixService(IQaWorkspaceResolver workspaces,IRepositoryTechnologyDetector detector,IQaAdapterRegistry adapters):IVerifiedFixService
{
    public async Task<VerifiedFixResult> VerifyAsync(VerifiedFixRequest request,CancellationToken ct)
    {
        var source=workspaces.Resolve(request.WorkspaceRelativePath);var worktreeAdded=false;var root=Path.Combine(Path.GetTempPath(),"dip-fix-verification",Guid.NewGuid().ToString("N"));var work=Path.Combine(root,"workspace");var artifact=Path.Combine(root,"artifacts");Directory.CreateDirectory(root);Directory.CreateDirectory(artifact);var steps=new List<string>();var findings=new List<QaFindingCandidate>();string? commit=null;
        try
        {
            if(request.RegressionTests.Count==0)return Fail("Verified Fix requires at least one regression-test draft; a patch alone cannot become Verified.");
            var policyViolation=PatchPolicyViolation(request.UnifiedDiff);if(policyViolation is not null)return Fail(policyViolation);
            var git=ProcessExecutor.FindExecutable("git");if(git is null)return Fail("Git is required for isolated fix verification.");
            if(Directory.Exists(Path.Combine(source,".git"))||File.Exists(Path.Combine(source,".git")))
            {
                var target=string.IsNullOrWhiteSpace(request.BaseCommit)?"HEAD":request.BaseCommit!;var add=await Run(git,["worktree","add","--detach",work,target],source,ct);steps.Add("git worktree add");if(add.ExitCode==0)worktreeAdded=true;if(add.ExitCode!=0)return Fail("Unable to create verification worktree: "+Last(add.Error));commit=(await Run(git,["rev-parse","HEAD"],work,ct)).Output.Trim();
            }
            else
            {
                CopyTree(source,work);var init=await Run(git,["init","-q"],work,ct);if(init.ExitCode!=0)return Fail("Unable to initialize temporary verification repository.");steps.Add("isolated source copy");
            }
            var patch=Path.Combine(root,"proposal.diff");await File.WriteAllTextAsync(patch,request.UnifiedDiff,Encoding.UTF8,ct);
            var check=await Run(git,["apply","--check","--whitespace=error",patch],work,ct);steps.Add("git apply --check");if(check.ExitCode!=0)return Fail("Proposed patch does not apply cleanly: "+Last(check.Error));
            var apply=await Run(git,["apply","--whitespace=error",patch],work,ct);if(apply.ExitCode!=0)return Fail("Patch application failed: "+Last(apply.Error));steps.Add("patch applied in isolated worktree");
            foreach(var test in request.RegressionTests){var path=ResolveInside(work,test.RelativePath);Directory.CreateDirectory(Path.GetDirectoryName(path)!);await File.WriteAllTextAsync(path,test.Content,Encoding.UTF8,ct);steps.Add("regression test: "+test.RelativePath);}
            var profile=await detector.DetectAsync(work,ct);var requested=request.Language.IsUnknown?LanguageId.Auto:request.Language;var selected=adapters.Resolve(requested,profile,work);if(selected.Count==0)return Fail("No QA adapter can verify the proposed fix for the detected technology.");
            var allToolsSucceeded=true;var executed=0;
            foreach(var selection in selected)
            {
                var result=await selection.Adapter.RunAsync(new(request.ProjectId,$"verified-fix:{selection.Language.Value}",selection.Language,".",commit,null,null,Math.Clamp(request.TimeoutSeconds,30,1800),false,false,false),work,Path.Combine(artifact,selection.Language.Value),ct);executed++;findings.AddRange(result.Findings);foreach(var tool in result.Tools.Where(x=>x.WasAvailable))if(!tool.Succeeded)allToolsSucceeded=false;steps.Add($"{selection.Language.Value}: {selection.Adapter.AdapterId} ({result.Tools.Count} tool(s))");
            }
            var verified=executed>0&&allToolsSucceeded&&!findings.Any(x=>x.Severity==Severity.Critical);var status=verified?"Verified":"NotVerified";var summary=verified?"Patch and regression tests passed all available QA validation in the isolated worktree.":"Patch was evaluated, but one or more QA tools failed or a Critical finding remains.";
            await File.WriteAllTextAsync(Path.Combine(artifact,"verified-fix-summary.txt"),$"Status={status}\nBaseCommit={commit}\nSteps={string.Join("; ",steps)}\nCritical={findings.Count(x=>x.Severity==Severity.Critical)}\n",ct);
            return new(true,verified,status,commit,artifact,steps,findings.Take(2000).ToArray(),summary);
        }
        finally
        {
            // Keep artifacts for this response/request; only temporary worktree contents are removed.
            try
            {
                if(worktreeAdded)
                {
                    var git=ProcessExecutor.FindExecutable("git");if(git is not null)await Run(git,["worktree","remove","--force",work],source,CancellationToken.None);
                }
                else if(Directory.Exists(work))Directory.Delete(work,true);
            }
            catch{}
        }

        VerifiedFixResult Fail(string message){try{File.WriteAllText(Path.Combine(artifact,"verified-fix-summary.txt"),"Status=Failed\n"+message);}catch{}return new(false,false,"Failed",commit,artifact,steps,findings,message);}
    }


    private static string? PatchPolicyViolation(string diff)
    {
        if(string.IsNullOrWhiteSpace(diff)||!diff.Contains("diff --git ",StringComparison.Ordinal))return "Verified Fix requires a standard unified git diff.";
        var forbidden=new[]{"pragma warning disable","eslint-disable","noinspection","@SuppressWarnings","@Disabled","@Ignore","pytest.skip","unittest.skip","test.skip(","describe.skip(","Assert.Ignore","TreatWarningsAsErrors>false","NoWarn>"};
        foreach(var line in diff.Split(['\r','\n'],StringSplitOptions.RemoveEmptyEntries))
        {
            if(!line.StartsWith('+')||line.StartsWith("+++",StringComparison.Ordinal))continue;
            if(forbidden.Any(token=>line.Contains(token,StringComparison.OrdinalIgnoreCase)))return $"Patch policy rejected a diagnostic/test suppression token: {forbidden.First(token=>line.Contains(token,StringComparison.OrdinalIgnoreCase))}.";
        }
        return null;
    }
    private static string ResolveInside(string root,string relative){if(string.IsNullOrWhiteSpace(relative)||Path.IsPathRooted(relative))throw new InvalidOperationException("Regression test path must be workspace-relative.");var r=Path.GetFullPath(root);var p=Path.GetFullPath(Path.Combine(r,relative));var prefix=r.TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar;if(!p.StartsWith(prefix,OperatingSystem.IsWindows()?StringComparison.OrdinalIgnoreCase:StringComparison.Ordinal))throw new InvalidOperationException("Regression test path escapes the verification worktree.");return p;}
    private static void CopyTree(string source,string dest){Directory.CreateDirectory(dest);var ignored=new HashSet<string>(new[]{".git","bin","obj","artifacts","node_modules","Library","Temp","Logs","target",".dipqa"},StringComparer.OrdinalIgnoreCase);var stack=new Stack<(string S,string D)>();stack.Push((source,dest));while(stack.Count>0){var (s,d)=stack.Pop();foreach(var f in Directory.EnumerateFiles(s))File.Copy(f,Path.Combine(d,Path.GetFileName(f)),true);foreach(var child in Directory.EnumerateDirectories(s)){if(ignored.Contains(Path.GetFileName(child)))continue;var nd=Path.Combine(d,Path.GetFileName(child));Directory.CreateDirectory(nd);stack.Push((child,nd));}}}
    private static async Task<(int ExitCode,string Output,string Error)> Run(string exe,IEnumerable<string> args,string cwd,CancellationToken ct){var psi=new ProcessStartInfo(exe){WorkingDirectory=cwd,UseShellExecute=false,RedirectStandardOutput=true,RedirectStandardError=true,CreateNoWindow=true};foreach(var a in args)psi.ArgumentList.Add(a);using var p=Process.Start(psi)??throw new InvalidOperationException("Unable to start git.");var o=p.StandardOutput.ReadToEndAsync(ct);var e=p.StandardError.ReadToEndAsync(ct);await p.WaitForExitAsync(ct);return(p.ExitCode,await o,await e);}
    private static string Last(string text)=>text.Split(['\r','\n'],StringSplitOptions.RemoveEmptyEntries).LastOrDefault()??text;
}
