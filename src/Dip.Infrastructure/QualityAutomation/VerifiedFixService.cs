using System.Diagnostics;
using System.Text;
using Dip.Application.Features.QualityAutomation;
using Dip.Domain.Enums;
using Dip.Domain.ValueObjects;

namespace Dip.Infrastructure.QualityAutomation;

/// <summary>
/// Verifies a proposed patch in an isolated temporary worktree/copy. The source
/// workspace is never modified. Verification includes a baseline QA snapshot,
/// patch policy checks, generated regression tests, a second QA run and a
/// before/after regression comparison.
/// </summary>
public sealed class VerifiedFixService(IQaWorkspaceResolver workspaces,IRepositoryTechnologyDetector detector,IQaAdapterRegistry adapters):IVerifiedFixService
{
    public async Task<VerifiedFixResult> VerifyAsync(VerifiedFixRequest request,CancellationToken ct)
    {
        var source=workspaces.Resolve(request.WorkspaceRelativePath);var worktreeAdded=false;var root=Path.Combine(Path.GetTempPath(),"dip-fix-verification",Guid.NewGuid().ToString("N"));var work=Path.Combine(root,"workspace");var artifact=Path.Combine(root,"artifacts");Directory.CreateDirectory(root);Directory.CreateDirectory(artifact);var steps=new List<string>();var findings=new List<QaFindingCandidate>();string? commit=null;
        try
        {
            if(request.RegressionTests.Count is <1 or >20)return Fail("Verified Fix requires 1-20 regression-test drafts; a patch alone cannot become Verified.");
            if(request.UnifiedDiff.Length>2_000_000)return Fail("Proposed patch exceeds the 2 MB verification limit.");
            if(request.RegressionTests.Sum(x=>x.Content.Length)>1_000_000)return Fail("Generated regression tests exceed the 1 MB total limit.");
            var policyViolation=PatchPolicyViolation(request.UnifiedDiff);if(policyViolation is not null)return Fail(policyViolation);
            var git=ProcessExecutor.FindExecutable("git");if(git is null)return Fail("Git is required for isolated fix verification.");

            if(Directory.Exists(Path.Combine(source,".git"))||File.Exists(Path.Combine(source,".git")))
            {
                var target=string.IsNullOrWhiteSpace(request.BaseCommit)?"HEAD":request.BaseCommit!;
                var add=await Run(git,["worktree","add","--detach",work,target],source,ct);steps.Add("git worktree add");if(add.ExitCode==0)worktreeAdded=true;if(add.ExitCode!=0)return Fail("Unable to create verification worktree: "+Last(add.Error));commit=(await Run(git,["rev-parse","HEAD"],work,ct)).Output.Trim();
            }
            else
            {
                CopyTree(source,work);var init=await Run(git,["init","-q"],work,ct);if(init.ExitCode!=0)return Fail("Unable to initialize temporary verification repository.");steps.Add("isolated source copy");
            }

            var requested=request.Language.IsUnknown?LanguageId.Auto:request.Language;
            var baselineProfile=await detector.DetectAsync(work,ct);
            var baselineSelections=adapters.Resolve(requested,baselineProfile,work);if(baselineSelections.Count==0)return Fail("No QA adapter can establish a baseline for the detected technology.");
            var baseline=await RunQaSetAsync(request,baselineSelections,work,Path.Combine(artifact,"baseline"),commit,"verified-fix-baseline",ct);steps.Add($"baseline QA: {baseline.ExecutedAdapters} adapter(s), {baseline.AvailableTools.Count} available tool(s)");

            var patch=Path.Combine(root,"proposal.diff");await File.WriteAllTextAsync(patch,request.UnifiedDiff,Encoding.UTF8,ct);
            var check=await Run(git,["apply","--check","--whitespace=error",patch],work,ct);steps.Add("git apply --check");if(check.ExitCode!=0)return Fail("Proposed patch does not apply cleanly: "+Last(check.Error));
            var apply=await Run(git,["apply","--whitespace=error",patch],work,ct);if(apply.ExitCode!=0)return Fail("Patch application failed: "+Last(apply.Error));steps.Add("patch applied in isolated worktree");

            foreach(var test in request.RegressionTests)
            {
                if(test.Content.Length>256_000)return Fail($"Regression test '{test.RelativePath}' exceeds the 256 KB per-file limit.");
                var path=ResolveInside(work,test.RelativePath);Directory.CreateDirectory(Path.GetDirectoryName(path)!);await File.WriteAllTextAsync(path,test.Content,Encoding.UTF8,ct);steps.Add("regression test: "+test.RelativePath);
            }

            var postProfile=await detector.DetectAsync(work,ct);var postSelections=adapters.Resolve(requested,postProfile,work);if(postSelections.Count==0)return Fail("The patch removed or invalidated all QA adapter coverage.");
            var post=await RunQaSetAsync(request,postSelections,work,Path.Combine(artifact,"post"),commit,"verified-fix",ct);findings.AddRange(post.Findings);steps.Add($"post-patch QA: {post.ExecutedAdapters} adapter(s), {post.AvailableTools.Count} available tool(s)");

            var missingTools=baseline.AvailableTools.Except(post.AvailableTools,StringComparer.OrdinalIgnoreCase).OrderBy(x=>x).ToArray();
            if(missingTools.Length>0)return Fail("Patch reduced verification coverage by making previously available tools disappear: "+string.Join(", ",missingTools));

            var baselineCritical=baseline.CriticalFingerprints;
            var postCritical=post.CriticalFingerprints;
            var newCritical=postCritical.Except(baselineCritical,StringComparer.OrdinalIgnoreCase).ToArray();
            var allToolsSucceeded=post.ToolExecutions.Where(x=>x.WasAvailable).All(x=>x.Succeeded);
            var verified=post.ExecutedAdapters>0&&allToolsSucceeded&&newCritical.Length==0;
            var status=verified?"Verified":"NotVerified";
            var summary=verified
                ?"Patch and regression tests passed available QA without reducing tool coverage or introducing a new Critical fingerprint. Human review is still required."
                :$"Patch was evaluated but did not satisfy the verification boundary (toolSuccess={allToolsSucceeded}, newCritical={newCritical.Length}).";

            await File.WriteAllTextAsync(Path.Combine(artifact,"verified-fix-summary.txt"),
                $"Status={status}\nBaseCommit={commit}\nBaselineCritical={baselineCritical.Count}\nPostCritical={postCritical.Count}\nNewCritical={newCritical.Length}\nMissingTools={string.Join(",",missingTools)}\nSteps={string.Join("; ",steps)}\n",ct);
            return new(true,verified,status,commit,artifact,steps,findings.Take(2000).ToArray(),summary);
        }
        finally
        {
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

    private async Task<QaSnapshot> RunQaSetAsync(
        VerifiedFixRequest request,IReadOnlyList<QaAdapterSelection> selections,string workspace,string artifactRoot,
        string? commit,string subjectPrefix,CancellationToken ct)
    {
        Directory.CreateDirectory(artifactRoot);var findings=new List<(LanguageId Language,QaFindingCandidate Finding)>();var tools=new List<QaToolExecution>();var executed=0;
        foreach(var selection in selections)
        {
            var result=await selection.Adapter.RunAsync(
                new(request.ProjectId,$"{subjectPrefix}:{selection.Language.Value}",selection.Language,".",commit,null,null,Math.Clamp(request.TimeoutSeconds,30,1800),false,false,false),
                workspace,Path.Combine(artifactRoot,selection.Language.Value),ct);
            executed++;tools.AddRange(result.Tools);findings.AddRange(result.Findings.Select(x=>(selection.Language,x)));
        }
        var available=tools.Where(x=>x.WasAvailable).Select(x=>x.Tool).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var critical=findings.Where(x=>x.Finding.Severity==Severity.Critical).Select(x=>QaFingerprint.Create(x.Language,x.Finding)).ToHashSet(StringComparer.OrdinalIgnoreCase);
        return new(executed,tools,findings.Select(x=>x.Finding).ToArray(),available,critical);
    }

    private static string? PatchPolicyViolation(string diff)
    {
        if(string.IsNullOrWhiteSpace(diff)||!diff.Contains("diff --git ",StringComparison.Ordinal))return "Verified Fix requires a standard unified git diff.";
        var forbiddenTokens=new[]{"pragma warning disable","eslint-disable","noinspection","@SuppressWarnings","@Disabled","@Ignore","pytest.skip","unittest.skip","test.skip(","describe.skip(","Assert.Ignore","TreatWarningsAsErrors>false","NoWarn>"};
        var forbiddenPaths=new[]{".github/workflows/",".env","secrets.","secret.","appsettings.production.json"};
        var hasNonTestChange=false;
        foreach(var line in diff.Split(['\r','\n'],StringSplitOptions.RemoveEmptyEntries))
        {
            if(line.StartsWith("+++ b/",StringComparison.Ordinal))
            {
                var path=line[6..].Replace('\\','/').ToLowerInvariant();
                if(forbiddenPaths.Any(token=>path.Contains(token,StringComparison.OrdinalIgnoreCase)))return $"Patch policy rejected protected path '{path}'.";
                if(!IsTestPath(path))hasNonTestChange=true;
            }
            if(!line.StartsWith('+')||line.StartsWith("+++",StringComparison.Ordinal))continue;
            var token=forbiddenTokens.FirstOrDefault(t=>line.Contains(t,StringComparison.OrdinalIgnoreCase));if(token is not null)return $"Patch policy rejected a diagnostic/test suppression token: {token}.";
        }
        if(!hasNonTestChange)return "Verified Fix requires at least one non-test source/configuration change; a generated test-only patch is not a fix.";
        return null;
    }

    private static bool IsTestPath(string path)
        =>path.Contains("/test",StringComparison.OrdinalIgnoreCase)||path.StartsWith("test",StringComparison.OrdinalIgnoreCase)||path.Contains("/spec",StringComparison.OrdinalIgnoreCase)||path.EndsWith(".spec.ts",StringComparison.OrdinalIgnoreCase)||path.EndsWith(".test.ts",StringComparison.OrdinalIgnoreCase);

    private static string ResolveInside(string root,string relative){if(string.IsNullOrWhiteSpace(relative)||Path.IsPathRooted(relative))throw new InvalidOperationException("Regression test path must be workspace-relative.");var r=Path.GetFullPath(root);var p=Path.GetFullPath(Path.Combine(r,relative));var prefix=r.TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar;if(!p.StartsWith(prefix,OperatingSystem.IsWindows()?StringComparison.OrdinalIgnoreCase:StringComparison.Ordinal))throw new InvalidOperationException("Regression test path escapes the verification worktree.");return p;}
    private static void CopyTree(string source,string dest){Directory.CreateDirectory(dest);var ignored=new HashSet<string>(new[]{".git","bin","obj","artifacts","node_modules","Library","Temp","Logs","target",".dipqa"},StringComparer.OrdinalIgnoreCase);var stack=new Stack<(string S,string D)>();stack.Push((source,dest));while(stack.Count>0){var (s,d)=stack.Pop();foreach(var f in Directory.EnumerateFiles(s))File.Copy(f,Path.Combine(d,Path.GetFileName(f)),true);foreach(var child in Directory.EnumerateDirectories(s)){if(ignored.Contains(Path.GetFileName(child)))continue;var nd=Path.Combine(d,Path.GetFileName(child));Directory.CreateDirectory(nd);stack.Push((child,nd));}}}
    private static async Task<(int ExitCode,string Output,string Error)> Run(string exe,IEnumerable<string> args,string cwd,CancellationToken ct){var psi=new ProcessStartInfo(exe){WorkingDirectory=cwd,UseShellExecute=false,RedirectStandardOutput=true,RedirectStandardError=true,CreateNoWindow=true};foreach(var a in args)psi.ArgumentList.Add(a);using var p=Process.Start(psi)??throw new InvalidOperationException("Unable to start git.");var o=p.StandardOutput.ReadToEndAsync(ct);var e=p.StandardError.ReadToEndAsync(ct);await p.WaitForExitAsync(ct);return(p.ExitCode,await o,await e);}
    private static string Last(string text)=>text.Split(['\r','\n'],StringSplitOptions.RemoveEmptyEntries).LastOrDefault()??text;

    private sealed record QaSnapshot(
        int ExecutedAdapters,IReadOnlyList<QaToolExecution> ToolExecutions,IReadOnlyList<QaFindingCandidate> Findings,
        IReadOnlySet<string> AvailableTools,IReadOnlySet<string> CriticalFingerprints);
}
