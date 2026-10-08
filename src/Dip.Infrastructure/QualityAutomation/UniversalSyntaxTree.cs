// Purpose: Builds a dependency-free universal syntax tree with balanced groups, calls, assignments, identifiers, literals, and member operators.
namespace Dip.Infrastructure.QualityAutomation;

public enum UniversalSyntaxKind { Root, Block, Group, Index, Call, Assignment, Identifier, Literal, MemberAccess, Operator, Token }
public sealed record UniversalSyntaxNode(UniversalSyntaxKind Kind, string Text, int Offset, int Length, IReadOnlyList<UniversalSyntaxNode> Children);

public static class UniversalSyntaxTree
{
    private sealed record Tok(string Text, int Offset, TokenClass Class);
    private enum TokenClass { Identifier, Literal, Operator, Token }

    public static UniversalSyntaxNode Parse(string source)
    {
        ArgumentNullException.ThrowIfNull(source);
        var tokens = Lex(source); var root = new Mutable(UniversalSyntaxKind.Root, "root", 0); var stack = new Stack<Mutable>(); stack.Push(root);
        for (var i = 0; i < tokens.Count; i++)
        {
            var t = tokens[i]; var current = stack.Peek();
            if (t.Text is "{" or "(" or "[")
            {
                var kind = t.Text switch { "{" => UniversalSyntaxKind.Block, "[" => UniversalSyntaxKind.Index, _ when i > 0 && tokens[i - 1].Class == TokenClass.Identifier => UniversalSyntaxKind.Call, _ => UniversalSyntaxKind.Group };
                var node = new Mutable(kind, t.Text, t.Offset); current.Children.Add(node); stack.Push(node); continue;
            }
            if (t.Text is "}" or ")" or "]") { if (stack.Count > 1) { var n = stack.Pop(); n.End = t.Offset + t.Text.Length; } continue; }
            if (IsAssignment(t.Text) && i > 0 && tokens[i - 1].Class == TokenClass.Identifier)
            {
                current.Children.Add(new Mutable(UniversalSyntaxKind.Assignment, tokens[i - 1].Text, tokens[i - 1].Offset) { End = t.Offset + t.Text.Length }); continue;
            }
            if (t.Text is "." or "->" or "::") { current.Children.Add(new Mutable(UniversalSyntaxKind.MemberAccess, t.Text, t.Offset) { End = t.Offset + t.Text.Length }); continue; }
            var kindForToken = t.Class switch { TokenClass.Identifier => UniversalSyntaxKind.Identifier, TokenClass.Literal => UniversalSyntaxKind.Literal, TokenClass.Operator => UniversalSyntaxKind.Operator, _ => UniversalSyntaxKind.Token };
            current.Children.Add(new Mutable(kindForToken, t.Text, t.Offset) { End = t.Offset + t.Text.Length });
        }
        while (stack.Count > 1) { var n = stack.Pop(); n.End = source.Length; }
        root.End = source.Length; return Freeze(root);
    }

    public static int CountNodes(UniversalSyntaxNode node) => 1 + node.Children.Sum(CountNodes);
    public static IEnumerable<UniversalSyntaxNode> Descendants(UniversalSyntaxNode node)
    {
        foreach (var child in node.Children) { yield return child; foreach (var nested in Descendants(child)) yield return nested; }
    }

    private static bool IsAssignment(string token) => token is "=" or "+=" or "-=" or "*=" or "/=" or "%=" or ":=";
    private static UniversalSyntaxNode Freeze(Mutable n) => new(n.Kind, n.Text, n.Start, Math.Max(0, n.End - n.Start), n.Children.Select(Freeze).ToArray());
    private sealed class Mutable(UniversalSyntaxKind kind, string text, int start) { public UniversalSyntaxKind Kind = kind; public string Text = text; public int Start = start; public int End = start + text.Length; public List<Mutable> Children = new(); }

    private static List<Tok> Lex(string s)
    {
        var result = new List<Tok>();
        for (var i = 0; i < s.Length;)
        {
            var c = s[i];
            if (char.IsWhiteSpace(c)) { i++; continue; }
            if (c == '/' && i + 1 < s.Length && s[i + 1] == '/') { while (i < s.Length && s[i] != '\n') i++; continue; }
            if (c == '#') { while (i < s.Length && s[i] != '\n') i++; continue; }
            if (c == '/' && i + 1 < s.Length && s[i + 1] == '*') { i += 2; while (i + 1 < s.Length && !(s[i] == '*' && s[i + 1] == '/')) i++; i = Math.Min(s.Length, i + 2); continue; }
            if (c is '"' or '\'')
            {
                var q = c; var start = i++; var escape = false;
                while (i < s.Length) { var x = s[i++]; if (escape) { escape = false; continue; } if (x == '\\') { escape = true; continue; } if (x == q) break; }
                result.Add(new(s[start..i], start, TokenClass.Literal)); continue;
            }
            if (char.IsLetter(c) || c is '_' or '$') { var start = i++; while (i < s.Length && (char.IsLetterOrDigit(s[i]) || s[i] is '_' or '$')) i++; result.Add(new(s[start..i], start, TokenClass.Identifier)); continue; }
            if (char.IsDigit(c)) { var start = i++; while (i < s.Length && (char.IsLetterOrDigit(s[i]) || s[i] is '.' or '_')) i++; result.Add(new(s[start..i], start, TokenClass.Literal)); continue; }
            var two = i + 1 < s.Length ? s.Substring(i, 2) : string.Empty;
            if (two is "->" or "::" or "==" or "!=" or "<=" or ">=" or "&&" or "||" or "+=" or "-=" or "*=" or "/=" or "%=" or ":=") { result.Add(new(two, i, TokenClass.Operator)); i += 2; continue; }
            result.Add(new(c.ToString(), i, "=+-*/%<>!&|^?:.".Contains(c) ? TokenClass.Operator : TokenClass.Token)); i++;
        }
        return result;
    }
}
