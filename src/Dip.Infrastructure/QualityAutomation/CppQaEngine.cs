using Dip.Application.Features.QualityAutomation;
using Dip.Domain.Enums;
using Dip.Domain.ValueObjects;

namespace Dip.Infrastructure.QualityAutomation;

public sealed class CppQaEngine(ProcessExecutor executor):IQaAdapter,ICppHardcodedQaRunner
{
    public LanguageId Language=>LanguageId.Cpp;
    public int Priority=>100;
    public async Task<HardcodedQaResult> RunAsync(QaExecutionCommand command,string workspace,string artifactRoot,CancellationToken ct)
    {
        var cmake=ProcessExecutor.FindExecutable("cmake")??throw new FileNotFoundException("CMake was not found in PATH.");
        var tools=new List<QaToolExecution>();var findings=new List<QaFindingCandidate>();var timeout=TimeSpan.FromSeconds(Math.Clamp(command.TimeoutSeconds,5,1800));
        var sanitizerEnvironment=new Dictionary<string,string>{{"ASAN_OPTIONS",OperatingSystem.IsWindows()?"halt_on_error=1":"detect_leaks=1:halt_on_error=1"},{"UBSAN_OPTIONS","print_stacktrace=1:halt_on_error=0"}};
        var build=Path.Combine(workspace,".dipqa","asan-build");Directory.CreateDirectory(build);
        var flags=OperatingSystem.IsWindows()?"/W4 /fsanitize=address /Zi":"-Wall -Wextra -Wpedantic -fsanitize=address,undefined -fno-omit-frame-pointer -g";
        await Run("cmake-configure",cmake,["-S",workspace,"-B",build,"-DCMAKE_BUILD_TYPE=RelWithDebInfo","-DCMAKE_EXPORT_COMPILE_COMMANDS=ON",$"-DCMAKE_CXX_FLAGS={flags}"],Severity.Critical,"configure");
        await Run("cmake-build",cmake,["--build",build,"--config","RelWithDebInfo","--parallel"],Severity.Critical,"compile");
        var ctest=ProcessExecutor.FindExecutable("ctest");if(ctest is not null)await Run("ctest",ctest,["--test-dir",build,"-C","RelWithDebInfo","--output-on-failure"],Severity.Warning,"test",sanitizerEnvironment);else AddUnavailable("ctest");
        var clangTidy=ProcessExecutor.FindExecutable("clang-tidy");
        if(clangTidy is not null&&File.Exists(Path.Combine(build,"compile_commands.json")))await RunClangTidy(clangTidy,build);
        else if(clangTidy is null)AddUnavailable("clang-tidy");
        else tools.Add(new("clang-tidy",false,false,null,0,string.Empty,string.Empty,"clang-tidy skipped because the selected CMake generator did not emit compile_commands.json."));
        var cppcheck=ProcessExecutor.FindExecutable("cppcheck");if(cppcheck is not null)await Run("cppcheck",cppcheck,["--enable=warning,performance,portability","--inline-suppr","--quiet",workspace],Severity.Warning,"static-analysis");else AddUnavailable("cppcheck");
        if(!string.IsNullOrWhiteSpace(command.RuntimeExecutable))
        {
            var executable=Path.IsPathRooted(command.RuntimeExecutable!)?command.RuntimeExecutable!:Path.GetFullPath(Path.Combine(workspace,command.RuntimeExecutable!));
            await Run("cpp-runtime-asan",executable,command.RuntimeArguments??Array.Empty<string>(),Severity.Critical,"runtime",sanitizerEnvironment);
        }
        return new(Language,tools,findings,QaToolSupport.JsonSummary(Language,tools,findings));
        async Task Run(string name,string executable,IEnumerable<string> args,Severity fallback,string category,IReadOnlyDictionary<string,string>? environment=null)
        {
            var result=await QaToolSupport.ExecuteAsync(executor,name,executable,args,workspace,artifactRoot,environment,timeout,Language,fallback,category,ct);
            tools.Add(result.Tool);
            if(name.Equals("ctest",StringComparison.OrdinalIgnoreCase)&&result.Tool.Succeeded)
                findings.AddRange(result.Findings.Where(x=>!x.Category.Equals("test",StringComparison.OrdinalIgnoreCase)));
            else
                findings.AddRange(result.Findings);
        }
        void AddUnavailable(string name)=>tools.Add(new(name,false,false,null,0,string.Empty,string.Empty,$"{name} is unavailable; optional analysis was skipped."));
        async Task RunClangTidy(string executable,string buildDir)
        {
            var sources=Directory.EnumerateFiles(workspace,"*.*",SearchOption.AllDirectories).Where(x=>x.EndsWith(".cpp",StringComparison.OrdinalIgnoreCase)||x.EndsWith(".cc",StringComparison.OrdinalIgnoreCase)||x.EndsWith(".cxx",StringComparison.OrdinalIgnoreCase)).Where(x=>!x.Contains(Path.DirectorySeparatorChar+".dipqa"+Path.DirectorySeparatorChar)).Take(100).ToArray();
            foreach(var source in sources){var result=await QaToolSupport.ExecuteAsync(executor,"clang-tidy",executable,[source,"-p",buildDir,"--quiet"],workspace,artifactRoot,null,timeout,Language,Severity.Warning,"static-analysis",ct);tools.Add(result.Tool);findings.AddRange(result.Findings);}
        }
    }
}
