using System.Text.Json;
using System.Text.RegularExpressions;
using Dip.Application.Features.P2Intelligence;
using Dip.Application.Features.QualityAutomation;
using Dip.Infrastructure.QualityAutomation;

namespace Dip.Infrastructure.P2Intelligence;

public sealed class NativeSymbolicator(
    ProcessExecutor executor,
    IQaWorkspaceResolver workspaceResolver) : INativeSymbolicator
{
    public async Task<NativeSymbolicationResult> SymbolicateAsync(
        NativeSymbolicationCommand command,CancellationToken cancellationToken)
    {
        var workspace=workspaceResolver.Resolve(command.WorkspaceRelativePath);
        var binary=SafePath(workspace,command.BinaryRelativePath);
        var root=ArtifactRoot(command.ProjectId);
        if(!File.Exists(binary))
            return Failure("none",Array.Empty<NativeSymbolicatedFrame>(),root,
                "Native binary was not found inside the configured QA workspace.");

        var addresses=(command.Addresses??Array.Empty<string>())
            .Where(x=>!string.IsNullOrWhiteSpace(x))
            .Select(x=>x.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(256)
            .ToArray();
        if(addresses.Length==0)
            return Failure("none",Array.Empty<NativeSymbolicatedFrame>(),root,"No native addresses were supplied.");

        var llvm=ProcessExecutor.FindExecutable("llvm-symbolizer")
                 ??ProcessExecutor.FindExecutable("llvm-symbolizer.exe");
        var addr2line=ProcessExecutor.FindExecutable("addr2line")
                      ??ProcessExecutor.FindExecutable("addr2line.exe");

        if(llvm is null&&addr2line is null)
            return Failure("none",addresses.Select(x=>new NativeSymbolicatedFrame(x,null,null,null,"")).ToArray(),
                root,"Neither llvm-symbolizer nor addr2line is available in PATH.");

        var tool=llvm is not null?"llvm-symbolizer":"addr2line";
        var executable=llvm??addr2line!;
        var frames=new List<NativeSymbolicatedFrame>();

        for(var i=0;i<addresses.Length;i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var address=addresses[i];
            var stdout=Path.Combine(root,$"frame-{i:D3}.out.log");
            var stderr=Path.Combine(root,$"frame-{i:D3}.err.log");
            IReadOnlyList<string> args=tool=="llvm-symbolizer"
                ?["--demangle","--functions","--inlines","-e",binary,address]
                :["-f","-C","-e",binary,address];

            var result=await executor.RunAsync(
                executable,args,workspace,null,TimeSpan.FromSeconds(15),stdout,stderr,cancellationToken);
            if(result.TimedOut||result.ExitCode is null)
            {
                frames.Add(new(address,null,null,null,result.StandardError));
                continue;
            }

            frames.Add(Parse(address,result.StandardOutput+"\n"+result.StandardError));
        }

        var resolved=frames.Count(x=>!string.IsNullOrWhiteSpace(x.Function)||!string.IsNullOrWhiteSpace(x.File));
        var status=resolved==0?"InfrastructureFailure":resolved==frames.Count?"Resolved":"Partial";
        var summary=$"Native symbolication resolved {resolved}/{frames.Count} frame(s) using {tool}.";
        var artifact=RelativeArtifact(root);
        await File.WriteAllTextAsync(
            Path.Combine(root,"symbolication.json"),
            JsonSerializer.Serialize(new{status,tool,summary,frames},new JsonSerializerOptions{WriteIndented=true}),
            cancellationToken);
        return new(status,tool,frames,artifact,summary);
    }

    private static NativeSymbolicatedFrame Parse(string address,string raw)
    {
        var lines=raw.Split(['\r','\n'],StringSplitOptions.RemoveEmptyEntries|StringSplitOptions.TrimEntries)
            .Where(x=>!x.StartsWith("warning:",StringComparison.OrdinalIgnoreCase))
            .Take(12).ToArray();
        var function=lines.FirstOrDefault(x=>!LooksLikeLocation(x)&&x!="??");
        var location=lines.FirstOrDefault(LooksLikeLocation);
        string? file=null;int? line=null;
        if(location is not null)
        {
            var match=Regex.Match(location,@"^(.*):(\d+)(?::\d+)?$");
            if(match.Success&&int.TryParse(match.Groups[2].Value,out var parsed))
            {
                file=match.Groups[1].Value;line=parsed;
            }
            else file=location;
        }
        if(function=="??")function=null;
        if(file=="??"||file=="??:0")file=null;
        return new(address,function,file,line,raw.Trim());
    }

    private static bool LooksLikeLocation(string value)
    {
        if(value.Contains(":?",StringComparison.Ordinal))return true;
        if(Regex.IsMatch(value,@"^(.*):(\d+)(?::\d+)?$"))return true;
        return value.Contains('/',StringComparison.Ordinal)
               ||value.Contains('\\',StringComparison.Ordinal);
    }

    private static NativeSymbolicationResult Failure(
        string tool,IReadOnlyList<NativeSymbolicatedFrame> frames,string root,string summary)
    {
        var payload=JsonSerializer.Serialize(new{status="InfrastructureFailure",tool,summary,frames},
            new JsonSerializerOptions{WriteIndented=true});
        File.WriteAllText(Path.Combine(root,"symbolication.json"),payload);
        return new("InfrastructureFailure",tool,frames,RelativeArtifact(root),summary);
    }

    private static string SafePath(string root,string relative)
    {
        if(Path.IsPathRooted(relative))throw new InvalidOperationException("Native binary path must be workspace-relative.");
        var full=Path.GetFullPath(Path.Combine(root,relative));
        var prefix=Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar;
        if(!full.StartsWith(prefix,StringComparison.OrdinalIgnoreCase)
           &&!full.Equals(Path.GetFullPath(root),StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Native binary path escapes the configured QA workspace.");
        return full;
    }

    private static string ArtifactRoot(Guid projectId)
    {
        var root=Path.GetFullPath(Path.Combine(
            "artifacts","p2",projectId.ToString("N"),"symbolication",
            $"{DateTimeOffset.UtcNow:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}"));
        Directory.CreateDirectory(root);
        return root;
    }

    private static string RelativeArtifact(string full)=>
        Path.GetRelativePath(Path.GetFullPath("."),full).Replace('\\','/');
}
