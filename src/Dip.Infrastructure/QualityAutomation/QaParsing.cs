using System.Text.RegularExpressions;
using Dip.Application.Features.QualityAutomation;
using Dip.Domain.Enums;

namespace Dip.Infrastructure.QualityAutomation;

internal static partial class QaParsing
{
    public static IReadOnlyList<QaFindingCandidate> ParsePython(string tool,string text,bool timedOut)
    {
        var list=new List<QaFindingCandidate>();
        if(timedOut)list.Add(new("hang",Severity.Critical,tool,"Python process timed out","The Python command exceeded its QA timeout.",ExceptionType:"Timeout"));
        foreach(Match m in PythonTracebackRegex().Matches(text))
            list.Add(new("runtime",Severity.Critical,tool,$"Unhandled {m.Groups[4].Value}",m.Groups[5].Value.Trim(),m.Groups[1].Value,int.Parse(m.Groups[2].Value),Function:m.Groups[3].Value,ExceptionType:m.Groups[4].Value,StackTrace:Trim(text)));
        foreach(Match m in PythonSyntaxRegex().Matches(text))
            list.Add(new("syntax",Severity.Critical,tool,m.Groups[3].Value,m.Groups[4].Value.Trim(),m.Groups[1].Value,int.Parse(m.Groups[2].Value),ExceptionType:m.Groups[3].Value));
        if(text.Contains("ModuleNotFoundError",StringComparison.Ordinal))list.Add(new("import",Severity.Critical,tool,"Python module import failed",LastLine(text),ExceptionType:"ModuleNotFoundError",StackTrace:Trim(text)));
        if(text.Contains("ImportError",StringComparison.Ordinal)&&!list.Any(x=>x.Category=="import"))list.Add(new("import",Severity.Critical,tool,"Python import failed",LastLine(text),ExceptionType:"ImportError",StackTrace:Trim(text)));
        if(text.Contains("was never awaited",StringComparison.OrdinalIgnoreCase)||text.Contains("Task was destroyed but it is pending",StringComparison.OrdinalIgnoreCase))
            list.Add(new("async",Severity.Warning,tool,"Python asynchronous work was not completed correctly",LastLineContaining(text,"await"),ExceptionType:"AsyncLifecycleWarning",StackTrace:Trim(text),RuleId:"python-asyncio-debug"));
        foreach(Match m in PytestFailureRegex().Matches(text))list.Add(new("test",Severity.Warning,tool,"pytest failure",m.Groups[1].Value.Trim(),RuleId:"pytest"));
        return Deduplicate(list);
    }
    public static IReadOnlyList<QaFindingCandidate> ParseCSharp(string tool,string text,bool timedOut)
    {
        var list=new List<QaFindingCandidate>();
        if(timedOut)list.Add(new("hang",Severity.Critical,tool,".NET process timed out","The .NET command exceeded its QA timeout.",ExceptionType:"Timeout"));
        foreach(Match m in MsBuildRegex().Matches(text))
            list.Add(new("compile",Severity.Critical,tool,$"C# {m.Groups[4].Value}",m.Groups[5].Value.Trim(),m.Groups[1].Value,ParseInt(m.Groups[2].Value),ParseInt(m.Groups[3].Value),RuleId:m.Groups[4].Value));
        foreach(Match m in MsBuildWarningRegex().Matches(text))
        {
            var rule=m.Groups[4].Value;var category=rule is "CS4014" or "CS1998"?"async":rule.StartsWith("CS86",StringComparison.Ordinal)?"nullability":"static-analysis";
            list.Add(new(category,Severity.Warning,tool,$"C# {rule} warning",m.Groups[5].Value.Trim(),m.Groups[1].Value,ParseInt(m.Groups[2].Value),ParseInt(m.Groups[3].Value),RuleId:rule));
        }
        foreach(Match m in DotNetExceptionRegex().Matches(text))
            list.Add(new("runtime",Severity.Critical,tool,$"Unhandled {m.Groups[1].Value}",m.Groups[2].Value.Trim(),ExceptionType:m.Groups[1].Value,StackTrace:Trim(text)));
        foreach(Match m in DotNetStackFrameRegex().Matches(text))
        {
            if(list.Count==0)break;
            var last=list[^1];
            if(last.FilePath is null)list[^1]=last with{Function=m.Groups[1].Value,FilePath=m.Groups[2].Value,Line=ParseInt(m.Groups[3].Value)};
        }
        if(text.Contains("Failed!",StringComparison.OrdinalIgnoreCase)||text.Contains("Failed:",StringComparison.OrdinalIgnoreCase))list.Add(new("test",Severity.Warning,tool,".NET tests failed",LastLineContaining(text,"Failed"),RuleId:"dotnet-test"));
        return Deduplicate(list);
    }
    public static IReadOnlyList<QaFindingCandidate> ParseCpp(string tool,string text,bool timedOut)
    {
        var list=new List<QaFindingCandidate>();
        if(timedOut)list.Add(new("hang",Severity.Critical,tool,"Native process timed out","The native command exceeded its QA timeout.",ExceptionType:"Timeout"));
        foreach(Match m in CppCompilerRegex().Matches(text))
            list.Add(new("compile",Severity.Critical,tool,$"C++ compiler {m.Groups[4].Value}",m.Groups[5].Value.Trim(),m.Groups[1].Value,ParseInt(m.Groups[2].Value),ParseInt(m.Groups[3].Value),RuleId:m.Groups[4].Value));
        foreach(Match m in GccCompilerRegex().Matches(text))
            list.Add(new("compile",Severity.Critical,tool,"C++ compiler error",m.Groups[4].Value.Trim(),m.Groups[1].Value,ParseInt(m.Groups[2].Value),ParseInt(m.Groups[3].Value),RuleId:"compiler"));
        var sanitizer=SanitizerKind(text);
        if(sanitizer is not null)
        {
            var frame=AsanSourceFrameRegex().Match(text);
            list.Add(new("memory-safety",Severity.Critical,tool,$"AddressSanitizer: {sanitizer}",FirstSanitizerLine(text),
                frame.Success?frame.Groups[2].Value:null,frame.Success?ParseInt(frame.Groups[3].Value):null,
                Function:frame.Success?frame.Groups[1].Value.Trim():null,ExceptionType:sanitizer,StackTrace:Trim(text),RuleId:"asan"));
        }
        if(text.Contains("LeakSanitizer",StringComparison.OrdinalIgnoreCase)&&text.Contains("detected memory leaks",StringComparison.OrdinalIgnoreCase))
            list.Add(new("memory-leak",Severity.Warning,tool,"LeakSanitizer detected live allocations at process exit",LastLineContaining(text,"SUMMARY"),ExceptionType:"MemoryLeak",StackTrace:Trim(text),RuleId:"lsan"));
        if(text.Contains("runtime error:",StringComparison.OrdinalIgnoreCase))
            list.Add(new("undefined-behavior",Severity.Critical,tool,"UndefinedBehaviorSanitizer reported invalid native behavior",LastLineContaining(text,"runtime error:"),ExceptionType:"UndefinedBehavior",StackTrace:Trim(text),RuleId:"ubsan"));
        if(text.Contains("Access violation",StringComparison.OrdinalIgnoreCase)||text.Contains("0xC0000005",StringComparison.OrdinalIgnoreCase))list.Add(new("native-crash",Severity.Critical,tool,"Native access violation",LastLine(text),ExceptionType:"AccessViolation",StackTrace:Trim(text)));
        if(text.Contains("tests failed",StringComparison.OrdinalIgnoreCase)||text.Contains("FAILED",StringComparison.Ordinal)&&tool.Contains("ctest",StringComparison.OrdinalIgnoreCase))list.Add(new("test",Severity.Warning,tool,"C++ tests failed",LastLineContaining(text,"fail"),RuleId:"ctest"));
        return Deduplicate(list);
    }
    public static IReadOnlyList<QaFindingCandidate> ParseForLanguage(QaLanguage language,string tool,string stdout,string stderr,bool timedOut=false)
    {
        var text=stdout+"\n"+stderr;
        return language switch{QaLanguage.Python=>ParsePython(tool,text,timedOut),QaLanguage.CSharp=>ParseCSharp(tool,text,timedOut),QaLanguage.Cpp=>ParseCpp(tool,text,timedOut),_=>Array.Empty<QaFindingCandidate>()};
    }
    public static bool MatchesFingerprint(Dip.Domain.Enums.QaLanguage language,string expected,string stdout,string stderr,bool timedOut=false)
        =>ParseForLanguage(language,"reproduction",stdout,stderr,timedOut).Any(x=>QaFingerprint.Create(language,x)==expected);
    private static IReadOnlyList<QaFindingCandidate> Deduplicate(IEnumerable<QaFindingCandidate> list)=>list.GroupBy(x=>string.Join('|',x.Category,x.FilePath,x.Line,x.ExceptionType,x.Message),StringComparer.Ordinal).Select(x=>x.First()).Take(200).ToArray();
    private static string Trim(string text)=>text.Length<=30000?text:text[..30000];
    private static string LastLine(string text)=>text.Split(['\r','\n'],StringSplitOptions.RemoveEmptyEntries).LastOrDefault()?.Trim()??"Process failed.";
    private static string LastLineContaining(string text,string token)=>text.Split(['\r','\n'],StringSplitOptions.RemoveEmptyEntries).LastOrDefault(x=>x.Contains(token,StringComparison.OrdinalIgnoreCase))?.Trim()??LastLine(text);
    private static int? ParseInt(string value)=>int.TryParse(value,out var n)?n:null;
    private static string? SanitizerKind(string text)=>new[]{"heap-use-after-free","stack-use-after-return","heap-buffer-overflow","stack-buffer-overflow","double-free","attempting free","use-after-poison","global-buffer-overflow"}.FirstOrDefault(x=>text.Contains(x,StringComparison.OrdinalIgnoreCase));
    private static string FirstSanitizerLine(string text)=>text.Split(['\r','\n'],StringSplitOptions.RemoveEmptyEntries).FirstOrDefault(x=>x.Contains("AddressSanitizer",StringComparison.OrdinalIgnoreCase))?.Trim()??"AddressSanitizer reported a memory-safety error.";

    [GeneratedRegex("File \\\"([^\\\"]+)\\\", line (\\d+), in ([^\\r\\n]+)[\\s\\S]*?([A-Za-z_][A-Za-z0-9_]*(?:Error|Exception)):\\s*([^\\r\\n]+)",RegexOptions.Multiline)] private static partial Regex PythonTracebackRegex();
    [GeneratedRegex("File \\\"([^\\\"]+)\\\", line (\\d+)[\\s\\S]*?\\n(?:\\s*\\^+\\s*\\n)?(SyntaxError|IndentationError|TabError):\\s*([^\\r\\n]+)",RegexOptions.Multiline)] private static partial Regex PythonSyntaxRegex();
    [GeneratedRegex("(?:FAILED|ERROR)\\s+([^\\r\\n]+)")] private static partial Regex PytestFailureRegex();
    [GeneratedRegex("^(.+\\.cs)\\((\\d+),(\\d+)\\):\\s+error\\s+([A-Z]+\\d+):\\s+(.+)$",RegexOptions.Multiline|RegexOptions.IgnoreCase)] private static partial Regex MsBuildRegex();
    [GeneratedRegex("^(.+\\.cs)\\((\\d+),(\\d+)\\):\\s+warning\\s+([A-Z]+\\d+):\\s+(.+)$",RegexOptions.Multiline|RegexOptions.IgnoreCase)] private static partial Regex MsBuildWarningRegex();
    [GeneratedRegex("(?:Unhandled exception\\.\\s*)?([A-Za-z0-9_.]+Exception):\\s*([^\\r\\n]+)")] private static partial Regex DotNetExceptionRegex();
    [GeneratedRegex("at\\s+(.+?)\\s+in\\s+(.+?):line\\s+(\\d+)")] private static partial Regex DotNetStackFrameRegex();
    [GeneratedRegex("^(.+?)[(:](\\d+)[,:](\\d+)[)]?:?\\s*(?:fatal\\s+)?error\\s+([^: ]+):?\\s*(.+)$",RegexOptions.Multiline|RegexOptions.IgnoreCase)] private static partial Regex CppCompilerRegex();
    [GeneratedRegex("^(.+?):(\\d+):(\\d+):\\s+(?:fatal\\s+)?error:\\s+(.+)$",RegexOptions.Multiline|RegexOptions.IgnoreCase)] private static partial Regex GccCompilerRegex();
    [GeneratedRegex("#\\d+\\s+.*?in\\s+([^\\r\\n]+?)\\s+([^\\s:]+\\.(?:c|cc|cpp|cxx|h|hpp)):(\\d+)(?::\\d+)?",RegexOptions.Multiline|RegexOptions.IgnoreCase)] private static partial Regex AsanSourceFrameRegex();
}
