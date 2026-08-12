using System.Diagnostics;
using Dip.Application.Abstractions.Integrations;
using Dip.Application.Features.DevelopmentGraph;
using Dip.Domain.Enums;
using Microsoft.Extensions.Options;

namespace Dip.Infrastructure.Integrations;

public sealed class PerforceVersionControlConnector:IVersionControlConnector
{
    private readonly PerforceOptions _options;public VersionControlProvider Provider=>VersionControlProvider.Perforce;
    public PerforceVersionControlConnector(IOptions<PerforceOptions> options){_options=options.Value;}
    public async Task<NormalizedVersionControlChange> FetchAsync(VersionControlSyncRequest request,CancellationToken ct)
    {
        var digits=new string(request.Revision.Where(char.IsDigit).ToArray());if(!int.TryParse(digits,out var change)||change<=0)throw new ArgumentException("Perforce revision must contain a positive changelist number.",nameof(request));var psi=new ProcessStartInfo(_options.Executable){RedirectStandardOutput=true,RedirectStandardError=true,UseShellExecute=false,CreateNoWindow=true,WorkingDirectory=string.IsNullOrWhiteSpace(request.WorkingDirectory)?_options.WorkingDirectory??Environment.CurrentDirectory:request.WorkingDirectory};psi.ArgumentList.Add("-ztag");psi.ArgumentList.Add("describe");psi.ArgumentList.Add("-s");psi.ArgumentList.Add(change.ToString(System.Globalization.CultureInfo.InvariantCulture));using var process=new Process{StartInfo=psi};process.Start();var stdoutTask=process.StandardOutput.ReadToEndAsync(ct);var stderrTask=process.StandardError.ReadToEndAsync(ct);using var timeout=CancellationTokenSource.CreateLinkedTokenSource(ct);timeout.CancelAfter(TimeSpan.FromSeconds(Math.Clamp(_options.TimeoutSeconds,5,300)));try{await process.WaitForExitAsync(timeout.Token);}catch(OperationCanceledException){try{process.Kill(true);}catch{}throw;}var stdout=await stdoutTask;var stderr=await stderrTask;if(process.ExitCode!=0)throw new InvalidOperationException($"Perforce describe failed ({process.ExitCode}): {stderr.Trim()}");
        var values=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);foreach(var line in stdout.Split(['\r','\n'],StringSplitOptions.RemoveEmptyEntries)){if(!line.StartsWith("... ",StringComparison.Ordinal))continue;var payload=line[4..];var space=payload.IndexOf(' ');if(space<=0)continue;values[payload[..space]]=payload[(space+1)..].Trim();}
        var files=new List<NormalizedChangedFile>();for(var i=0;i<5000;i++){if(!values.TryGetValue($"depotFile{i}",out var path))break;values.TryGetValue($"action{i}",out var action);files.Add(new(path,action??"edit"));}var title=values.TryGetValue("desc",out var desc)?desc:$"Perforce changelist {change}";var branch=Environment.GetEnvironmentVariable("P4CLIENT")??"perforce-workspace";var observed=values.TryGetValue("time",out var unix)&&long.TryParse(unix,out var epoch)?DateTimeOffset.FromUnixTimeSeconds(epoch):DateTimeOffset.UtcNow;return new(request.ProjectId,Provider,request.RepositoryKey,request.RepositoryLocator,change.ToString(System.Globalization.CultureInfo.InvariantCulture),branch,title,null,observed,files);
    }
}
