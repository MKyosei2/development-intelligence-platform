// Purpose: Performs conservative language-neutral semantic/data-flow analysis plus cross-file lock-order cycle detection.
using System.Text.RegularExpressions;
using Dip.Application.Features.QualityAutomation;
using Dip.Domain.Enums;
using Dip.Domain.ValueObjects;

namespace Dip.Infrastructure.QualityAutomation;

public sealed class UniversalSemanticAnalyzer : IUniversalSemanticAnalyzer
{
    private static readonly HashSet<string> Ignored = new(StringComparer.OrdinalIgnoreCase) { ".git", "bin", "obj", "node_modules", "artifacts", ".dipqa", "Library", "Temp", "Logs", "Build", "build", "vendor", "target" };
    private static readonly IReadOnlyDictionary<string, LanguageId> LanguageByExtension = new Dictionary<string, LanguageId>(StringComparer.OrdinalIgnoreCase)
    {
        [".py"] = LanguageId.Python, [".cs"] = LanguageId.CSharp, [".c"] = LanguageId.C, [".h"] = LanguageId.Cpp, [".cc"] = LanguageId.Cpp, [".cpp"] = LanguageId.Cpp, [".cxx"] = LanguageId.Cpp, [".hpp"] = LanguageId.Cpp,
        [".js"] = LanguageId.JavaScript, [".jsx"] = LanguageId.JavaScript, [".mjs"] = LanguageId.JavaScript, [".cjs"] = LanguageId.JavaScript, [".ts"] = LanguageId.TypeScript, [".tsx"] = LanguageId.TypeScript,
        [".java"] = LanguageId.Java, [".kt"] = LanguageId.Kotlin, [".kts"] = LanguageId.Kotlin, [".go"] = LanguageId.Go, [".rs"] = LanguageId.Rust, [".rb"] = LanguageId.Ruby, [".php"] = LanguageId.Php,
        [".swift"] = LanguageId.Swift, [".m"] = LanguageId.ObjectiveC, [".mm"] = LanguageId.ObjectiveC, [".lua"] = LanguageId.Lua, [".sh"] = LanguageId.Bash, [".ps1"] = LanguageId.PowerShell,
        [".gd"] = LanguageId.Gdscript, [".hlsl"] = LanguageId.Hlsl, [".compute"] = LanguageId.Hlsl, [".glsl"] = LanguageId.Glsl, [".vert"] = LanguageId.Glsl, [".frag"] = LanguageId.Glsl, [".geom"] = LanguageId.Glsl
    };

    private static readonly Regex Assignment = new(@"(?<name>[A-Za-z_][A-Za-z0-9_]*)\s*(?:=|:=)\s*(?<rhs>.+)", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex DangerousSink = new(@"(?<sink>eval|exec|system|popen|Process\.Start|Runtime\.getRuntime\(\)\.exec|child_process\.(?:exec|execSync)|os\.system|subprocess\.(?:run|Popen|call)|shell_exec|mysqli_query|executeQuery|ExecuteSqlRaw|FromSqlRaw)\s*\((?<arg>.+)", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static readonly Regex Source = new(@"(?<source>Request\.|HttpContext\.|Console\.ReadLine|Environment\.GetEnvironmentVariable|process\.argv|req\.(?:body|query|params)|input\(|sys\.argv|os\.environ|params\[|ARGV|\$_(?:GET|POST|REQUEST)|readLine\(|getenv\(|std::cin|scanf\s*\()", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static readonly Regex SecretLiteral = new("(?<key>api[_-]?key|secret|password|token)\\s*[:=]\\s*[\"'](?<value>[^\"']{8,})[\"']", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static readonly Regex EmptyCatch = new(@"catch\s*(?:\([^)]*\))?\s*\{\s*\}", RegexOptions.Compiled | RegexOptions.Singleline | RegexOptions.CultureInvariant);
    private static readonly Regex SqlConcat = new("(?:SELECT|INSERT|UPDATE|DELETE).{0,160}(?:\\+|\\$\\{|format\\(|f[\"'])", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static readonly Regex LockAcquire = new(@"(?:lock\s*\(\s*(?<cs>[A-Za-z_][A-Za-z0-9_\.]*)\s*\)|Monitor\.Enter\s*\(\s*(?<mon>[A-Za-z_][A-Za-z0-9_\.]*)|std::(?:unique_lock|lock_guard|scoped_lock)\s*<[^>]+>\s+[A-Za-z_][A-Za-z0-9_]*\s*\(\s*(?<cpp>[A-Za-z_][A-Za-z0-9_\.]*)|(?<java>[A-Za-z_][A-Za-z0-9_\.]*)\.lock\s*\(\s*\))", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public async Task<QaSemanticAnalysisResult> AnalyzeAsync(QaSemanticAnalysisRequest request, CancellationToken ct)
    {
        var findings = new List<QaFindingCandidate>(); var evidence = new List<string>();
        var lockEdges = new Dictionary<(string From, string To), List<(string File, int Line)>>();
        var files = EnumerateFiles(request.Workspace, request.Language).Take(Math.Clamp(request.MaximumFiles, 1, 5000)).ToArray();

        foreach (var file in files)
        {
            ct.ThrowIfCancellationRequested();
            string text; try { text = await File.ReadAllTextAsync(file, ct); } catch { continue; }
            if (text.Length > 2_000_000) continue;
            var relative = Path.GetRelativePath(request.Workspace, file).Replace('\\', '/');
            var lines = text.Split('\n'); var tree = UniversalSyntaxTree.Parse(text);
            var tainted = new Dictionary<string, (int Line, string Source)>(StringComparer.Ordinal);

            for (var i = 0; i < lines.Length; i++)
            {
                var line = lines[i]; var lineNo = i + 1; var assignment = Assignment.Match(line);
                if (assignment.Success)
                {
                    var rhs = assignment.Groups["rhs"].Value; var src = Source.Match(rhs);
                    if (src.Success) tainted[assignment.Groups["name"].Value] = (lineNo, src.Groups["source"].Value);
                    else foreach (var pair in tainted.Where(p => Regex.IsMatch(rhs, $@"\b{Regex.Escape(p.Key)}\b"))) tainted[assignment.Groups["name"].Value] = (lineNo, pair.Value.Source);
                }
                var sink = DangerousSink.Match(line);
                if (sink.Success)
                {
                    var arg = sink.Groups["arg"].Value; var direct = Source.Match(arg); var flow = tainted.FirstOrDefault(p => Regex.IsMatch(arg, $@"\b{Regex.Escape(p.Key)}\b"));
                    if (direct.Success || !string.IsNullOrEmpty(flow.Key))
                    {
                        var origin = direct.Success ? direct.Groups["source"].Value : $"{flow.Key} (tainted at line {flow.Value.Line} from {flow.Value.Source})";
                        findings.Add(F("data-flow", Severity.Critical, "Untrusted data reaches an execution/query sink", $"{origin} reaches {sink.Groups["sink"].Value} without an observed sanitizer in this bounded flow.", relative, lineNo, "DIP-DF001"));
                    }
                }
                var secret = SecretLiteral.Match(line);
                if (secret.Success && !LooksPlaceholder(secret.Groups["value"].Value)) findings.Add(F("security", Severity.Critical, "Possible hard-coded secret", $"A literal value is assigned to '{secret.Groups["key"].Value}'.", relative, lineNo, "DIP-SEC001"));
                if (SqlConcat.IsMatch(line)) findings.Add(F("data-flow", Severity.Warning, "Dynamic SQL construction detected", "Verify all external values are parameterized.", relative, lineNo, "DIP-DF002"));
                if ((line.Contains(".Result", StringComparison.Ordinal) || line.Contains(".Wait()", StringComparison.Ordinal)) && text.Contains("async ", StringComparison.Ordinal)) findings.Add(F("concurrency", Severity.Warning, "Sync-over-async deadlock risk", "Blocking on Task.Result/Wait in async code can deadlock synchronization contexts or exhaust worker threads.", relative, lineNo, "DIP-CON002"));
            }
            foreach (Match m in EmptyCatch.Matches(text).Cast<Match>().Take(20))
            {
                var lineNo = 1 + text[..m.Index].Count(c => c == '\n'); findings.Add(F("reliability", Severity.Warning, "Empty catch block suppresses failure evidence", "An exception is swallowed without logging, rethrowing, or explicit recovery.", relative, lineNo, "DIP-REL001"));
            }
            CaptureLockOrder(relative, lines, lockEdges);
            evidence.Add($"{relative}: ast-nodes={UniversalSyntaxTree.CountNodes(tree)}, lines={lines.Length}, taint-symbols={tainted.Count}");
        }

        AddLockCycleFindings(findings, lockEdges);
        var distinct = findings.GroupBy(x => string.Join('|', x.RuleId, x.FilePath, x.Line, x.Message), StringComparer.Ordinal).Select(x => x.First()).Take(2000).ToArray();
        return new(distinct, evidence.Take(200).ToArray(), files.Length);
    }

    private static void CaptureLockOrder(string file, IReadOnlyList<string> lines, IDictionary<(string From, string To), List<(string File, int Line)>> edges)
    {
        var held = new List<string>(); var braceDepth = 0; var scopeDepth = new Stack<int>();
        for (var i = 0; i < lines.Count; i++)
        {
            var line = lines[i]; var open = line.Count(c => c == '{'); var close = line.Count(c => c == '}');
            foreach (Match match in LockAcquire.Matches(line))
            {
                var name = new[] { match.Groups["cs"].Value, match.Groups["mon"].Value, match.Groups["cpp"].Value, match.Groups["java"].Value }.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));
                if (string.IsNullOrWhiteSpace(name)) continue;
                foreach (var previous in held)
                {
                    if (previous.Equals(name, StringComparison.Ordinal)) continue;
                    var key = (previous, name); if (!edges.TryGetValue(key, out var rows)) edges[key] = rows = new(); rows.Add((file, i + 1));
                }
                held.Add(name); scopeDepth.Push(braceDepth + Math.Max(1, open));
            }
            braceDepth += open - close;
            while (scopeDepth.Count > 0 && braceDepth < scopeDepth.Peek()) { scopeDepth.Pop(); if (held.Count > 0) held.RemoveAt(held.Count - 1); }
        }
    }

    private static void AddLockCycleFindings(ICollection<QaFindingCandidate> findings, IReadOnlyDictionary<(string From, string To), List<(string File, int Line)>> edges)
    {
        var emitted = new HashSet<string>(StringComparer.Ordinal);
        foreach (var edge in edges)
        {
            if (!edges.TryGetValue((edge.Key.To, edge.Key.From), out var reverse)) continue;
            var pair = string.Compare(edge.Key.From, edge.Key.To, StringComparison.Ordinal) <= 0 ? $"{edge.Key.From}<->{edge.Key.To}" : $"{edge.Key.To}<->{edge.Key.From}";
            if (!emitted.Add(pair)) continue;
            var first = edge.Value.First(); var second = reverse.First();
            findings.Add(F("concurrency", Severity.Critical, "Conflicting lock acquisition order", $"Observed {edge.Key.From}→{edge.Key.To} at {first.File}:{first.Line} and {edge.Key.To}→{edge.Key.From} at {second.File}:{second.Line}. This is a static lock-order cycle; runtime deadlock evidence is still required to prove an occurrence.", first.File, first.Line, "DIP-CON004"));
        }
    }

    private static QaFindingCandidate F(string category, Severity severity, string title, string message, string file, int line, string rule) => new(category, severity, "dip-semantic", title, message, file, line, RuleId: rule, SourceFormat: "dip-structural", SourceFormatVersion: "2.0");
    private static bool LooksPlaceholder(string value) => value.Contains("change-me", StringComparison.OrdinalIgnoreCase) || value.Contains("replace", StringComparison.OrdinalIgnoreCase) || value.Contains("example", StringComparison.OrdinalIgnoreCase) || value.All(c => c is '*' or 'x' or 'X');
    private static IEnumerable<string> EnumerateFiles(string root, LanguageId requestedLanguage)
    {
        var allLanguages = requestedLanguage.IsAuto || requestedLanguage.IsMulti || requestedLanguage.IsUnknown; var stack = new Stack<string>(); stack.Push(Path.GetFullPath(root));
        while (stack.Count > 0)
        {
            var dir = stack.Pop(); string[] dirs, files; try { dirs = Directory.GetDirectories(dir); files = Directory.GetFiles(dir); } catch { continue; }
            foreach (var file in files) { if (!LanguageByExtension.TryGetValue(Path.GetExtension(file), out var fileLanguage)) continue; if (allLanguages || fileLanguage == requestedLanguage) yield return file; }
            foreach (var child in dirs) if (!Ignored.Contains(Path.GetFileName(child))) stack.Push(child);
        }
    }
}
