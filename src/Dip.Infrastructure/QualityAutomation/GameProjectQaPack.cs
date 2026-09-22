// Purpose: Provides the infrastructure capability represented by G am eP ro je ct QA Pa ck.
using System.Text.Json;
using System.Text.RegularExpressions;
using Dip.Application.Features.QualityAutomation;
using Dip.Domain.Enums;

namespace Dip.Infrastructure.QualityAutomation;

/// <summary>Engine-aware repository checks that do not require an Editor installation.</summary>
public sealed class GameProjectQaPack:ICrossCuttingQaPack
{
    public string PackId=>"game-project";
    public bool Applies(QaTechnologyProfile profile,string workspace)=>profile.GameEngines.Count>0;
    public async Task<QaCrossCuttingResult> RunAsync(QaExecutionCommand command,QaTechnologyProfile profile,string workspace,string artifactRoot,CancellationToken ct)
    {
        var findings=new List<QaFindingCandidate>();var evidence=new List<string>();
        if(profile.GameEngines.Contains("Unity",StringComparer.OrdinalIgnoreCase))await Unity();
        if(profile.GameEngines.Contains("Unreal Engine",StringComparer.OrdinalIgnoreCase))await Unreal();
        if(profile.GameEngines.Contains("Godot",StringComparer.OrdinalIgnoreCase))await Godot();
        var summary=JsonSerializer.Serialize(new{pack=PackId,engines=profile.GameEngines,evidence,findingCount=findings.Count});
            Directory.CreateDirectory(artifactRoot);
            await File.WriteAllTextAsync(Path.Combine(artifactRoot,"game-project-qa.json"),summary,ct);
            return new(PackId,[new("game-project-static",true,true,0,0,"","",$"Engine-aware static QA completed for {string.Join(", ",profile.GameEngines)}." )],findings,summary);
        async Task Unity()
        {
            var editor=Path.Combine(workspace,"ProjectSettings","EditorSettings.asset");
                if(File.Exists(editor)){var text=await File.ReadAllTextAsync(editor,ct);
                if(text.Contains("m_SerializationMode: 0",StringComparison.Ordinal))findings.Add(F("unity-serialization",Severity.Warning,"Force Text serialization is not enabled","Text serialization improves deterministic asset diff/reference diagnostics; verify this is intentional.","ProjectSettings/EditorSettings.asset","DIP-UNITY-SERIALIZATION"));
                }
            var packages=Path.Combine(workspace,"Packages","manifest.json");
                if(File.Exists(packages)){var text=await File.ReadAllTextAsync(packages,ct);
                if(text.Contains("com.unity.addressables",StringComparison.OrdinalIgnoreCase)&&!Directory.Exists(Path.Combine(workspace,"Assets","AddressableAssetsData")))findings.Add(F("unity-addressables",Severity.Warning,"Addressables package detected without AddressableAssetsData","Verify Addressables initialization, profiles, and content build state.","Packages/manifest.json","DIP-UNITY-ADDRESSABLES"));
                }
            var logs=Directory.Exists(Path.Combine(workspace,"Logs"))?Directory.EnumerateFiles(Path.Combine(workspace,"Logs"),"*.log").Take(20):[];
                foreach(var log in logs){var text=await File.ReadAllTextAsync(log,ct);
                if(text.Contains("Shader error",StringComparison.OrdinalIgnoreCase)||text.Contains("Shader compiler",StringComparison.OrdinalIgnoreCase)&&text.Contains("error",StringComparison.OrdinalIgnoreCase))findings.Add(F("shader",Severity.Critical,"Unity shader compilation error observed","A Unity log contains shader compiler error evidence.",Path.GetRelativePath(workspace,log),"DIP-UNITY-SHADER"));
                }
            evidence.Add("Unity: ProjectSettings/Packages/log static checks");
        }
        async Task Unreal()
        {
            var project=Directory.EnumerateFiles(workspace,"*.uproject",SearchOption.TopDirectoryOnly).FirstOrDefault();
                if(project is null){findings.Add(F("unreal-project",Severity.Critical,"Unreal project descriptor is missing","Repository was detected as Unreal but no root .uproject was found.",".","DIP-UNREAL-UPROJECT"));
                return;
                }try{using var _=JsonDocument.Parse(await File.ReadAllTextAsync(project,ct));
                }
                catch(JsonException ex){findings.Add(F("unreal-project",Severity.Critical,"Invalid .uproject JSON",ex.Message,Path.GetFileName(project),"DIP-UNREAL-UPROJECT-JSON"));
                }
            var saved=Path.Combine(workspace,"Saved","Logs");
                if(Directory.Exists(saved))foreach(var log in Directory.EnumerateFiles(saved,"*.log").OrderByDescending(File.GetLastWriteTimeUtc).Take(10)){var lines=(await File.ReadAllLinesAsync(log,ct)).Where(x=>Regex.IsMatch(x,"Blueprint.*(?:Error|Compile failed)|PackagingResults.*Error|ShaderCompileWorker.*(?:Error|Failed)|LogNet.*(?:timeout|failure)",RegexOptions.IgnoreCase)).Take(100);
                foreach(var line in lines)findings.Add(F("unreal-runtime",Severity.Critical,"Unreal engine failure evidence",line.Trim(),Path.GetRelativePath(workspace,log),"DIP-UNREAL-LOG"));
                }
            evidence.Add("Unreal: descriptor + Blueprint/Packaging/Shader/Network log checks");
        }
        async Task Godot()
        {
            var project=Path.Combine(workspace,"project.godot");
                if(!File.Exists(project)){findings.Add(F("godot-project",Severity.Critical,"project.godot is missing","Godot was detected but its project descriptor is unavailable.",".","DIP-GODOT-PROJECT"));
                return;
                }var text=await File.ReadAllTextAsync(project,ct);
                if(!text.Contains("config_version",StringComparison.OrdinalIgnoreCase))findings.Add(F("godot-project",Severity.Warning,"Godot config_version not observed","Verify that project.godot is complete and generated by a supported engine version.","project.godot","DIP-GODOT-CONFIG"));
                evidence.Add("Godot: project descriptor static checks");
        }
    }
    private static QaFindingCandidate F(string category,Severity severity,string title,string message,string file,string rule)=>new(category,severity,"dip-game-qa",title,message,file,RuleId:rule,SourceFormat:"dip-game-qa",SourceFormatVersion:"1.0");
}
