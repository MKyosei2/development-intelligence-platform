// Purpose: Provides the R ep os it or yT ec hn ol og yD et ec to r infrastructure implementation.
using System.Text.Json;
using Dip.Application.Features.QualityAutomation;
using Dip.Domain.ValueObjects;

namespace Dip.Infrastructure.QualityAutomation;

public sealed class RepositoryTechnologyDetector:IRepositoryTechnologyDetector
{
    private static readonly IReadOnlyDictionary<string,LanguageId> Extensions=new Dictionary<string,LanguageId>(StringComparer.OrdinalIgnoreCase)
    {
        [".py"]=LanguageId.Python,[".cs"]=LanguageId.CSharp,[".c"]=LanguageId.C,[".cpp"]=LanguageId.Cpp,[".cc"]=LanguageId.Cpp,[".cxx"]=LanguageId.Cpp,[".hpp"]=LanguageId.Cpp,
        [".js"]=LanguageId.JavaScript,[".jsx"]=LanguageId.JavaScript,[".mjs"]=LanguageId.JavaScript,[".cjs"]=LanguageId.JavaScript,[".ts"]=LanguageId.TypeScript,[".tsx"]=LanguageId.TypeScript,
        [".java"]=LanguageId.Java,[".kt"]=LanguageId.Kotlin,[".kts"]=LanguageId.Kotlin,[".go"]=LanguageId.Go,[".rs"]=LanguageId.Rust,[".lua"]=LanguageId.Lua,[".rb"]=LanguageId.Ruby,[".php"]=LanguageId.Php,
        [".swift"]=LanguageId.Swift,[".m"]=LanguageId.ObjectiveC,[".mm"]=LanguageId.ObjectiveC,[".ps1"]=LanguageId.PowerShell,[".sh"]=LanguageId.Bash,[".sql"]=LanguageId.Sql,
        [".hlsl"]=LanguageId.Hlsl,[".compute"]=LanguageId.Hlsl,[".gd"]=LanguageId.Gdscript,[".glsl"]=LanguageId.Glsl,[".vert"]=LanguageId.Glsl,[".frag"]=LanguageId.Glsl,[".geom"]=LanguageId.Glsl
    };
    private static readonly HashSet<string> Ignored=new(StringComparer.OrdinalIgnoreCase){".git","bin","obj","node_modules",".dipqa","artifacts","Library","Temp","Logs","Build","build"};

    public Task<QaTechnologyProfile> DetectAsync(string workspace,CancellationToken cancellationToken)
    {
        var full=Path.GetFullPath(workspace);var counts=new Dictionary<LanguageId,int>();var evidence=new List<string>();
        var files=SafeFiles(full).Take(25000).ToArray();
        foreach(var file in files)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if(Extensions.TryGetValue(Path.GetExtension(file),out var language))counts[language]=counts.GetValueOrDefault(language)+1;
        }
        var builds=new HashSet<string>(StringComparer.OrdinalIgnoreCase);var frameworks=new HashSet<string>(StringComparer.OrdinalIgnoreCase);var engines=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        void Mark(string relative,string name,HashSet<string> target){if(File.Exists(Path.Combine(full,relative))||Directory.Exists(Path.Combine(full,relative))){target.Add(name);evidence.Add($"{name}: {relative}");}}
        Mark("CMakeLists.txt","CMake",builds);Mark("package.json","npm",builds);Mark("pyproject.toml","Python packaging",builds);Mark("requirements.txt","Python packaging",builds);Mark("Cargo.toml","Cargo",builds);Mark("go.mod","Go Modules",builds);Mark("pom.xml","Maven",builds);Mark("build.gradle","Gradle",builds);Mark("build.gradle.kts","Gradle",builds);Mark("Makefile","Make",builds);Mark("meson.build","Meson",builds);
        if(files.Any(x=>Path.GetExtension(x).Equals(".sln",StringComparison.OrdinalIgnoreCase)||Path.GetExtension(x).Equals(".csproj",StringComparison.OrdinalIgnoreCase))){builds.Add("MSBuild/.NET");evidence.Add("MSBuild/.NET project discovered");}
        if(Directory.Exists(Path.Combine(full,"Assets"))&&File.Exists(Path.Combine(full,"ProjectSettings","ProjectVersion.txt"))){engines.Add("Unity");frameworks.Add("Unity");evidence.Add("Unity: Assets + ProjectSettings/ProjectVersion.txt");}
        if(files.Any(x=>Path.GetExtension(x).Equals(".uproject",StringComparison.OrdinalIgnoreCase))){engines.Add("Unreal Engine");frameworks.Add("Unreal Engine");evidence.Add("Unreal Engine: .uproject");}
        Mark("project.godot","Godot",engines);if(engines.Contains("Godot"))frameworks.Add("Godot");
        DetectPackageFrameworks(full,frameworks,evidence);
        DetectManifestLanguages(full,counts,evidence);
        var languages=counts.OrderByDescending(x=>x.Value).ThenBy(x=>x.Key.Value,StringComparer.Ordinal).Select(x=>x.Key).ToArray();
        return Task.FromResult(new QaTechnologyProfile(full,languages,builds.OrderBy(x=>x).ToArray(),frameworks.OrderBy(x=>x).ToArray(),engines.OrderBy(x=>x).ToArray(),evidence.Take(100).ToArray()));
    }
    private static IEnumerable<string> SafeFiles(string root)
    {
        var stack=new Stack<string>();stack.Push(root);
        while(stack.Count>0){var dir=stack.Pop();string[] dirs;string[] files;try{dirs=Directory.GetDirectories(dir);files=Directory.GetFiles(dir);}catch{continue;}foreach(var f in files)yield return f;foreach(var d in dirs)if(!Ignored.Contains(Path.GetFileName(d)))stack.Push(d);}
    }
    private static void DetectPackageFrameworks(string root,HashSet<string> frameworks,List<string> evidence)
    {
        var package=Path.Combine(root,"package.json");if(File.Exists(package)){var text=File.ReadAllText(package);foreach(var name in new[]{"react","next","vue","svelte","express"})if(text.Contains($"\"{name}\"",StringComparison.OrdinalIgnoreCase)){frameworks.Add(name);evidence.Add($"framework: {name}");}}
        var requirements=new[]{Path.Combine(root,"requirements.txt"),Path.Combine(root,"pyproject.toml")}.Where(File.Exists);foreach(var path in requirements){var text=File.ReadAllText(path);foreach(var name in new[]{"fastapi","django","flask","pytest"})if(text.Contains(name,StringComparison.OrdinalIgnoreCase)){frameworks.Add(name);evidence.Add($"framework: {name}");}}
    }
    private static void DetectManifestLanguages(string root,Dictionary<LanguageId,int> counts,List<string> evidence)
    {
        var path=Path.Combine(root,".dip","qa.json");if(!File.Exists(path))return;
        try{using var doc=JsonDocument.Parse(File.ReadAllText(path));if(doc.RootElement.TryGetProperty("languages",out var arr)&&arr.ValueKind==JsonValueKind.Array)foreach(var item in arr.EnumerateArray())if(item.TryGetProperty("id",out var id)&&LanguageId.TryParse(id.GetString(),out var language)){counts[language]=Math.Max(1,counts.GetValueOrDefault(language));evidence.Add($"QA manifest language: {language.Value}");}}catch(JsonException ex){evidence.Add($"Invalid .dip/qa.json: {ex.Message}");}
    }
}
