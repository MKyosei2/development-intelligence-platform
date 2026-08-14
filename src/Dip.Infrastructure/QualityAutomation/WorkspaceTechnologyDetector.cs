using Dip.Application.Features.QualityAutomation;
using Dip.Domain.ValueObjects;

namespace Dip.Infrastructure.QualityAutomation;

public sealed class WorkspaceTechnologyDetector : IWorkspaceTechnologyDetector
{
    private static readonly HashSet<string> IgnoredDirectories = new(StringComparer.OrdinalIgnoreCase)
    {
        ".git","bin","obj","node_modules","Library","Temp","Logs","Build","Binaries","Intermediate",".gradle","target","vendor","dist","coverage"
    };

    private static readonly IReadOnlyDictionary<string,LanguageId> ExtensionLanguages = new Dictionary<string,LanguageId>(StringComparer.OrdinalIgnoreCase)
    {
        [".py"]=LanguageId.Python,[".pyw"]=LanguageId.Python,
        [".cs"]=LanguageId.CSharp,
        [".c"]=new("c"),[".h"]=new("c"),[".cc"]=LanguageId.Cpp,[".cpp"]=LanguageId.Cpp,[".cxx"]=LanguageId.Cpp,[".hpp"]=LanguageId.Cpp,[".hh"]=LanguageId.Cpp,
        [".js"]=LanguageId.JavaScript,[".mjs"]=LanguageId.JavaScript,[".cjs"]=LanguageId.JavaScript,
        [".ts"]=LanguageId.TypeScript,[".tsx"]=LanguageId.TypeScript,
        [".java"]=LanguageId.Java,[".kt"]=LanguageId.Kotlin,[".kts"]=LanguageId.Kotlin,
        [".go"]=LanguageId.Go,[".rs"]=LanguageId.Rust,[".lua"]=LanguageId.Lua,
        [".rb"]=new("ruby"),[".php"]=new("php"),[".swift"]=new("swift"),[".m"]=new("objective-c"),[".mm"]=new("objective-cpp"),
        [".ps1"]=new("powershell"),[".sh"]=new("shell"),[".sql"]=new("sql"),
        [".hlsl"]=LanguageId.Hlsl,[".hlsli"]=LanguageId.Hlsl,[".glsl"]=LanguageId.Glsl,[".vert"]=LanguageId.Glsl,[".frag"]=LanguageId.Glsl,[".comp"]=LanguageId.Glsl,
        [".gd"]=new("gdscript"),[".ex"]=new("elixir"),[".exs"]=new("elixir")
    };

    public Task<WorkspaceTechnologyProfile> DetectAsync(string workspace,CancellationToken cancellationToken)
    {
        var root=Path.GetFullPath(workspace);
        if(!Directory.Exists(root)) throw new DirectoryNotFoundException(root);

        var counts=new Dictionary<LanguageId,int>();
        var evidence=new List<string>();
        var files=EnumerateFiles(root,cancellationToken).Take(50000).ToArray();
        foreach(var file in files)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var ext=Path.GetExtension(file);
            if(ExtensionLanguages.TryGetValue(ext,out var language)) counts[language]=counts.GetValueOrDefault(language)+1;
        }

        foreach(var item in counts.OrderByDescending(x=>x.Value).ThenBy(x=>x.Key.Value,StringComparer.Ordinal).Take(12))
            evidence.Add($"language:{item.Key.Value}:{item.Value}-files");

        var builds=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var frameworks=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var engines=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        void Has(string pattern,string label,HashSet<string> target)
        {
            if(files.Any(f=>Path.GetFileName(f).Equals(pattern,StringComparison.OrdinalIgnoreCase)) || Directory.EnumerateFiles(root,pattern,SearchOption.TopDirectoryOnly).Any())
            { target.Add(label); evidence.Add($"detected:{label}"); }
        }

        Has("CMakeLists.txt","cmake",builds);
        if(files.Any(f=>f.EndsWith(".sln",StringComparison.OrdinalIgnoreCase)||f.EndsWith(".csproj",StringComparison.OrdinalIgnoreCase)||f.EndsWith(".vcxproj",StringComparison.OrdinalIgnoreCase))) { builds.Add("msbuild"); evidence.Add("detected:msbuild"); }
        Has("package.json","npm",builds);
        Has("pnpm-lock.yaml","pnpm",builds);
        Has("yarn.lock","yarn",builds);
        Has("Cargo.toml","cargo",builds);
        Has("go.mod","go-mod",builds);
        Has("pom.xml","maven",builds);
        if(files.Any(f=>Path.GetFileName(f).StartsWith("build.gradle",StringComparison.OrdinalIgnoreCase))) { builds.Add("gradle"); evidence.Add("detected:gradle"); }
        Has("pyproject.toml","python-pyproject",builds);
        Has("Makefile","make",builds);
        Has("BUILD.bazel","bazel",builds);

        var packageJson=files.FirstOrDefault(f=>Path.GetFileName(f).Equals("package.json",StringComparison.OrdinalIgnoreCase));
        if(packageJson is not null)
        {
            var text=SafeRead(packageJson);
            foreach(var name in new[]{"react","next","vue","angular","svelte","express","nestjs"})
                if(text.Contains($"\"{name}\"",StringComparison.OrdinalIgnoreCase)) frameworks.Add(name);
        }
        if(files.Any(f=>f.EndsWith(".uproject",StringComparison.OrdinalIgnoreCase))) { engines.Add("unreal"); evidence.Add("detected:unreal"); }
        if(Directory.Exists(Path.Combine(root,"Assets"))&&Directory.Exists(Path.Combine(root,"ProjectSettings"))) { engines.Add("unity"); evidence.Add("detected:unity"); }
        if(files.Any(f=>Path.GetFileName(f).Equals("project.godot",StringComparison.OrdinalIgnoreCase))) { engines.Add("godot"); evidence.Add("detected:godot"); }

        if(engines.Contains("unity")) frameworks.Add("unity");
        if(engines.Contains("unreal")) frameworks.Add("unreal-engine");
        if(files.Any(f=>Path.GetFileName(f).Equals("pytest.ini",StringComparison.OrdinalIgnoreCase))) frameworks.Add("pytest");
        if(files.Any(f=>Path.GetFileName(f).Equals("tsconfig.json",StringComparison.OrdinalIgnoreCase))) frameworks.Add("typescript");

        var languages=counts.OrderByDescending(x=>ScoreLanguage(x.Key,x.Value,engines)).ThenByDescending(x=>x.Value).Select(x=>x.Key).ToArray();
        return Task.FromResult(new WorkspaceTechnologyProfile(languages,builds.OrderBy(x=>x).ToArray(),frameworks.OrderBy(x=>x).ToArray(),engines.OrderBy(x=>x).ToArray(),evidence));
    }

    private static int ScoreLanguage(LanguageId language,int count,IReadOnlySet<string> engines)
    {
        var score=count;
        if(engines.Contains("unity")&&language==LanguageId.CSharp) score+=100000;
        if(engines.Contains("unreal")&&language==LanguageId.Cpp) score+=100000;
        if(language.Is("c")&&count>0) score-=1;
        return score;
    }

    private static IEnumerable<string> EnumerateFiles(string root,CancellationToken ct)
    {
        var stack=new Stack<string>(); stack.Push(root);
        while(stack.Count>0)
        {
            ct.ThrowIfCancellationRequested();
            var dir=stack.Pop();
            IEnumerable<string> children;
            try{children=Directory.EnumerateDirectories(dir);}catch{continue;}
            foreach(var child in children)
                if(!IgnoredDirectories.Contains(Path.GetFileName(child))) stack.Push(child);
            IEnumerable<string> files;
            try{files=Directory.EnumerateFiles(dir);}catch{continue;}
            foreach(var file in files) yield return file;
        }
    }

    private static string SafeRead(string path)
    {
        try{return File.ReadAllText(path);}catch{return string.Empty;}
    }
}
