// Purpose: Runs syntax-aware mutation experiments and records killed and surviving mutations without mutating comments or literals.
using System.Text.Json;
using Dip.Application.Features.P2Intelligence;
using Dip.Application.Features.QualityAutomation;
using Dip.Infrastructure.QualityAutomation;

namespace Dip.Infrastructure.P2Intelligence;

internal sealed class MutationExperimentRunner(ProcessExecutor executor, IQaWorkspaceResolver workspaceResolver)
    : ExperimentExecutionSupport(executor, workspaceResolver)
{
    private static readonly HashSet<string> SupportedSourceExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".cs", ".c", ".cc", ".cpp", ".cxx", ".h", ".hpp", ".java", ".kt", ".kts",
        ".js", ".jsx", ".ts", ".tsx", ".go", ".rs", ".swift", ".m", ".mm", ".php"
    };

    private static readonly MutationOperator[] Operators =
    [
        new(">=", ">", "ast-boundary-gte-to-gt", false),
        new("<=", "<", "ast-boundary-lte-to-lt", false),
        new("==", "!=", "ast-equality-invert", false),
        new("!=", "==", "ast-inequality-invert", false),
        new("&&", "||", "ast-and-to-or", false),
        new("||", "&&", "ast-or-to-and", false),
        new("true", "false", "ast-bool-true-to-false", true),
        new("false", "true", "ast-bool-false-to-true", true),
        new("+", "-", "ast-arithmetic-plus-to-minus", false),
        new("-", "+", "ast-arithmetic-minus-to-plus", false)
    ];

    public async Task<MutationResult> RunMutationAsync(MutationCommand command, CancellationToken ct)
    {
        var workspace = workspaceResolver.Resolve(command.Target.WorkspaceRelativePath);
        var root = ArtifactRoot(command.Target.ProjectId, "mutation");
        var temp = Path.Combine(root, "workspace");
        await PrepareIsolatedWorkspaceAsync(workspace, temp, ct);
        var executable = ResolveTargetExecutable(temp, command.Target.Executable);

        var baselineDirectory = Path.Combine(root, "baseline");
        Directory.CreateDirectory(baselineDirectory);
        var baseline = await executor.RunAsync(
            executable,
            command.Target.Arguments ?? Array.Empty<string>(),
            temp,
            null,
            TimeSpan.FromSeconds(Math.Clamp(command.Target.TimeoutSeconds, 1, 3600)),
            Path.Combine(baselineDirectory, "stdout.log"),
            Path.Combine(baselineDirectory, "stderr.log"),
            ct);
        var baselinePassed = !baseline.TimedOut && baseline.ExitCode == 0;
        if (!baselinePassed)
        {
            var detail = baseline.TimedOut
                ? "Baseline timed out; mutation score was not calculated."
                : $"Baseline exited with code {(baseline.ExitCode?.ToString() ?? "n/a")}; mutation score was not calculated.";
            await WriteManifestAsync(root, command, "InfrastructureFailure", 0, 0, 0, 0m, [], detail, ct);
            return new("InfrastructureFailure", 0, 0, 0, 0, 0m, Array.Empty<MutationCaseResult>(), RelativeArtifact(root), false, detail);
        }

        var requested = command.Mutations?.ToList() ?? new List<MutationCaseSpec>();
        var maxMutations = Math.Clamp(command.MaxMutations, 1, 200);
        if (command.GenerateCommonMutations && requested.Count < maxMutations)
        {
            requested.AddRange(BuildSyntaxAwareMutations(
                temp,
                command.AutoSourceFiles,
                maxMutations - requested.Count));
        }

        var unique = requested
            .Where(x => !string.IsNullOrWhiteSpace(x.Id)
                        && !string.IsNullOrWhiteSpace(x.FileRelativePath)
                        && x.OriginalText != x.ReplacementText)
            .GroupBy(x => x.Id, StringComparer.OrdinalIgnoreCase)
            .Select(x => x.First())
            .Take(maxMutations)
            .ToArray();

        var cases = new List<MutationCaseResult>();
        var infrastructureFailure = false;
        foreach (var mutation in unique)
        {
            ct.ThrowIfCancellationRequested();
            var file = SafePath(temp, mutation.FileRelativePath);
            if (!File.Exists(file))
            {
                cases.Add(new(mutation.Id, false, false, null, "File not found.", mutation.FileRelativePath, null, null, mutation.Operator));
                continue;
            }

            var original = await File.ReadAllTextAsync(file, ct);
            var offsets = FindAll(original, mutation.OriginalText, 3);
            if (offsets.Count == 0)
            {
                cases.Add(new(mutation.Id, false, false, null, "OriginalText was not found.", mutation.FileRelativePath, null, null, mutation.Operator));
                continue;
            }
            if (offsets.Count > 1)
            {
                cases.Add(new(mutation.Id, false, false, null, "Mutation anchor is ambiguous; the syntax-aware context occurs more than once.", mutation.FileRelativePath, null, null, mutation.Operator));
                continue;
            }

            var index = offsets[0];
            var (line, column) = LineColumn(original, index);
            var mutated = original[..index] + mutation.ReplacementText + original[(index + mutation.OriginalText.Length)..];
            var directory = Path.Combine(root, "cases", SafeName(mutation.Id));
            Directory.CreateDirectory(directory);
            try
            {
                await File.WriteAllTextAsync(file, mutated, ct);
                await File.WriteAllTextAsync(
                    Path.Combine(directory, "mutation.patch.txt"),
                    $"Strategy: universal-ast-aware-v2{Environment.NewLine}File: {mutation.FileRelativePath}{Environment.NewLine}Line: {line}, Column: {column}{Environment.NewLine}Operator: {mutation.Operator ?? "explicit"}{Environment.NewLine}- {mutation.OriginalText}{Environment.NewLine}+ {mutation.ReplacementText}{Environment.NewLine}",
                    ct);

                var result = await executor.RunAsync(
                    executable,
                    command.Target.Arguments ?? Array.Empty<string>(),
                    temp,
                    new Dictionary<string, string>
                    {
                        ["DIP_MUTATION"] = "1",
                        ["DIP_MUTATION_ID"] = mutation.Id,
                        ["DIP_MUTATION_OPERATOR"] = mutation.Operator ?? "explicit"
                    },
                    TimeSpan.FromSeconds(Math.Clamp(command.Target.TimeoutSeconds, 1, 3600)),
                    Path.Combine(directory, "stdout.log"),
                    Path.Combine(directory, "stderr.log"),
                    ct);
                if (!result.TimedOut && result.ExitCode is null)
                {
                    infrastructureFailure = true;
                    cases.Add(new(mutation.Id, true, false, null, "Infrastructure failure starting the configured test target.", mutation.FileRelativePath, line, column, mutation.Operator));
                    break;
                }

                var killed = result.TimedOut || (result.ExitCode.HasValue && result.ExitCode.Value != 0);
                cases.Add(new(
                    mutation.Id,
                    true,
                    killed,
                    result.ExitCode,
                    killed ? "Tests detected the AST-aware mutation." : "AST-aware mutation survived the configured tests.",
                    mutation.FileRelativePath,
                    line,
                    column,
                    mutation.Operator));
            }
            finally
            {
                await File.WriteAllTextAsync(file, original, CancellationToken.None);
            }
        }

        var applied = cases.Count(x => x.Applied);
        var killedCount = cases.Count(x => x.Applied && x.Killed);
        var survived = Math.Max(0, applied - killedCount);
        var score = applied == 0 ? 0m : decimal.Round((decimal)killedCount / applied * 100m, 2);
        var status = infrastructureFailure || applied == 0
            ? "InfrastructureFailure"
            : score >= 80m ? "Passed" : score >= 60m ? "Warning" : "Failed";
        var summary = $"Baseline passed. Universal-AST-aware mutation v2 killed {killedCount}/{applied} applied mutation(s); {survived} survived.";
        await WriteManifestAsync(root, command, status, cases.Count, applied, killedCount, score, cases, summary, ct);
        return new(status, cases.Count, applied, killedCount, survived, score, cases, RelativeArtifact(root), true, summary);
    }

    private static IReadOnlyList<MutationCaseSpec> BuildSyntaxAwareMutations(
        string workspace,
        IReadOnlyList<string>? sourceFiles,
        int remaining)
    {
        if (remaining <= 0)
            return Array.Empty<MutationCaseSpec>();

        var relativeFiles = sourceFiles is { Count: > 0 }
            ? sourceFiles.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray()
            : EnumerateCandidateSourceFiles(workspace).ToArray();

        var result = new List<MutationCaseSpec>();
        foreach (var relative in relativeFiles)
        {
            if (result.Count >= remaining)
                break;

            var full = SafePath(workspace, relative);
            if (!File.Exists(full) || !SupportedSourceExtensions.Contains(Path.GetExtension(full)))
                continue;

            string text;
            try
            {
                text = File.ReadAllText(full);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                continue;
            }
            if (text.Length == 0 || text.Length > 2_000_000)
                continue;

            var codeMask = BuildCodeMask(text);
            var syntaxTree = UniversalSyntaxTree.Parse(text);
            foreach (var op in Operators)
            {
                var search = 0;
                while (result.Count < remaining)
                {
                    var index = text.IndexOf(op.Old, search, StringComparison.Ordinal);
                    if (index < 0)
                        break;
                    search = index + Math.Max(1, op.Old.Length);

                    if (!IsCodeRange(codeMask, index, op.Old.Length))
                        continue;
                    if (op.WordBoundary && !HasWordBoundary(text, index, op.Old.Length))
                        continue;
                    if (!LooksLikeExpressionSite(text, codeMask, index, op.Old.Length, op.WordBoundary))
                        continue;
                    if (!AstCoversMutationSite(syntaxTree, index, op.Old.Length))
                        continue;
                    if (op.Name.Contains("arithmetic", StringComparison.Ordinal)
                        && IsUnaryOperatorSite(text, codeMask, index))
                        continue;

                    var context = UniqueContext(text, index, op.Old.Length);
                    if (context is null)
                        continue;
                    var localIndex = index - context.Value.Start;
                    var replacement = context.Value.Text[..localIndex]
                                      + op.New
                                      + context.Value.Text[(localIndex + op.Old.Length)..];
                    var (line, _) = LineColumn(text, index);
                    var normalizedRelative = Path.GetRelativePath(workspace, full).Replace('\\', '/');
                    result.Add(new(
                        $"ast-{op.Name}-{SafeName(Path.GetFileNameWithoutExtension(full))}-{line:D5}-{result.Count + 1:D3}",
                        normalizedRelative,
                        context.Value.Text,
                        replacement,
                        op.Name));
                }
            }
        }
        return result;
    }

    private static IEnumerable<string> EnumerateCandidateSourceFiles(string workspace)
    {
        var ignored = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".git", ".dipqa", "bin", "obj", "artifacts", "node_modules", "Library", "Temp", "Logs", "target", "vendor"
        };
        var stack = new Stack<string>();
        stack.Push(workspace);
        while (stack.Count > 0)
        {
            var dir = stack.Pop();
            string[] files;
            string[] directories;
            try
            {
                files = Directory.GetFiles(dir);
                directories = Directory.GetDirectories(dir);
            }
            catch
            {
                continue;
            }

            foreach (var file in files)
                if (SupportedSourceExtensions.Contains(Path.GetExtension(file)))
                    yield return Path.GetRelativePath(workspace, file).Replace('\\', '/');
            foreach (var child in directories)
                if (!ignored.Contains(Path.GetFileName(child)))
                    stack.Push(child);
        }
    }

    private static bool[] BuildCodeMask(string text)
    {
        var code = Enumerable.Repeat(true, text.Length).ToArray();
        var state = LexicalState.Code;
        var quote = '\0';
        for (var i = 0; i < text.Length; i++)
        {
            var ch = text[i];
            var next = i + 1 < text.Length ? text[i + 1] : '\0';
            switch (state)
            {
                case LexicalState.Code:
                    if (ch == '/' && next == '/')
                    {
                        code[i] = code[i + 1] = false;
                        state = LexicalState.LineComment;
                        i++;
                    }
                    else if (ch == '/' && next == '*')
                    {
                        code[i] = code[i + 1] = false;
                        state = LexicalState.BlockComment;
                        i++;
                    }
                    else if (ch is '\'' or '"' or '`')
                    {
                        quote = ch;
                        code[i] = false;
                        state = LexicalState.String;
                    }
                    break;

                case LexicalState.LineComment:
                    code[i] = false;
                    if (ch == '\n')
                        state = LexicalState.Code;
                    break;

                case LexicalState.BlockComment:
                    code[i] = false;
                    if (ch == '*' && next == '/')
                    {
                        code[i + 1] = false;
                        state = LexicalState.Code;
                        i++;
                    }
                    break;

                case LexicalState.String:
                    code[i] = false;
                    if (ch == '\\')
                    {
                        if (i + 1 < text.Length)
                        {
                            code[i + 1] = false;
                            i++;
                        }
                    }
                    else if (ch == quote)
                    {
                        state = LexicalState.Code;
                    }
                    break;
            }
        }
        return code;
    }

    private static bool IsCodeRange(bool[] mask, int start, int length)
    {
        if (start < 0 || length <= 0 || start + length > mask.Length)
            return false;
        for (var i = start; i < start + length; i++)
            if (!mask[i])
                return false;
        return true;
    }

    private static bool HasWordBoundary(string text, int start, int length)
    {
        static bool Word(char ch) => char.IsLetterOrDigit(ch) || ch == '_';
        var before = start > 0 ? text[start - 1] : '\0';
        var after = start + length < text.Length ? text[start + length] : '\0';
        return !Word(before) && !Word(after);
    }

    private static bool AstCoversMutationSite(UniversalSyntaxNode root, int start, int length)
    {
        var end = start + length;
        var leaves = new List<UniversalSyntaxNode>();
        CollectLeaves(root, leaves);
        var relevant = leaves
            .Where(node => node.Offset < end && node.Offset + Math.Max(1, node.Length) > start)
            .ToArray();
        return relevant.Length > 0
               && relevant.All(node => node.Kind != UniversalSyntaxKind.Literal)
               && relevant.Min(node => node.Offset) <= start
               && relevant.Max(node => node.Offset + Math.Max(1, node.Length)) >= end;
    }

    private static void CollectLeaves(UniversalSyntaxNode node, ICollection<UniversalSyntaxNode> output)
    {
        if (node.Children.Count == 0)
        {
            if (node.Kind != UniversalSyntaxKind.Root) output.Add(node);
            return;
        }
        foreach (var child in node.Children) CollectLeaves(child, output);
    }

    private static bool IsUnaryOperatorSite(string text, bool[] mask, int index)
    {
        var previous = PreviousCodeChar(text, mask, index - 1);
        if (previous is null) return true;
        return previous.Value is '(' or '[' or '{' or '=' or ',' or ':' or '?'
            or '+' or '-' or '*' or '/' or '%' or '!' or '&' or '|' or '^' or '<' or '>';
    }

    private static bool LooksLikeExpressionSite(string text, bool[] mask, int start, int length, bool literal)
    {
        if (literal)
            return true;

        var previous = PreviousCodeChar(text, mask, start - 1);
        var next = NextCodeChar(text, mask, start + length);
        if (previous is null || next is null)
            return false;

        // Avoid preprocessor/directive-like and obviously delimiter-only locations.
        if (previous is '#' or ';' or '{' || next is ';' or '}' or ',')
            return false;
        return true;
    }

    private static char? PreviousCodeChar(string text, bool[] mask, int start)
    {
        for (var i = Math.Min(start, text.Length - 1); i >= 0; i--)
            if (mask[i] && !char.IsWhiteSpace(text[i]))
                return text[i];
        return null;
    }

    private static char? NextCodeChar(string text, bool[] mask, int start)
    {
        for (var i = Math.Max(0, start); i < text.Length; i++)
            if (mask[i] && !char.IsWhiteSpace(text[i]))
                return text[i];
        return null;
    }

    private static (int Start, string Text)? UniqueContext(string text, int tokenIndex, int tokenLength)
    {
        foreach (var radius in new[] { 32, 64, 128, 256 })
        {
            var start = Math.Max(0, tokenIndex - radius);
            var end = Math.Min(text.Length, tokenIndex + tokenLength + radius);
            while (start > 0 && text[start - 1] != '\n' && tokenIndex - start < radius * 2)
                start--;
            while (end < text.Length && text[end] != '\n' && end - tokenIndex < radius * 2)
                end++;
            var context = text[start..end];
            if (FindAll(text, context, 2).Count == 1)
                return (start, context);
        }
        return null;
    }

    private static async Task WriteManifestAsync(
        string root,
        MutationCommand command,
        string status,
        int total,
        int applied,
        int killed,
        decimal score,
        IReadOnlyList<MutationCaseResult> cases,
        string summary,
        CancellationToken ct)
    {
        var payload = JsonSerializer.Serialize(new
        {
            schema = "dip.mutation.v2",
            strategy = "universal-ast-aware",
            status,
            baselineRequired = true,
            command.MaxMutations,
            command.GenerateCommonMutations,
            requestedSourceFiles = command.AutoSourceFiles,
            total,
            applied,
            killed,
            survived = Math.Max(0, applied - killed),
            mutationScorePercent = score,
            summary,
            cases
        }, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(Path.Combine(root, "mutation-manifest.json"), payload, ct);
    }

    private sealed record MutationOperator(string Old, string New, string Name, bool WordBoundary);

    private enum LexicalState
    {
        Code,
        LineComment,
        BlockComment,
        String
    }
}
