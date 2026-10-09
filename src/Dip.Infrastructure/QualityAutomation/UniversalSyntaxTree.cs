// Purpose: Builds a dependency-free universal syntax tree with balanced groups, calls, assignments, identifiers, literals, and member operators.
namespace Dip.Infrastructure.QualityAutomation;

public enum UniversalSyntaxKind
{
    Root,
    Block,
    Group,
    Index,
    Call,
    Assignment,
    Identifier,
    Literal,
    MemberAccess,
    Operator,
    Token
}

public sealed record UniversalSyntaxNode(
    UniversalSyntaxKind Kind,
    string Text,
    int Offset,
    int Length,
    IReadOnlyList<UniversalSyntaxNode> Children);

public static class UniversalSyntaxTree
{
    private sealed record Tok(
        string Text,
        int Offset,
        TokenClass Class);

    private enum TokenClass
    {
        Identifier,
        Literal,
        Operator,
        Token
    }

    public static UniversalSyntaxNode Parse(string source)
    {
        ArgumentNullException.ThrowIfNull(source);

        var tokens = Lex(source);
        var root = new Mutable(
            UniversalSyntaxKind.Root,
            "root",
            0);
        var stack = new Stack<Mutable>();
        stack.Push(root);

        for (var i = 0; i < tokens.Count; i++)
        {
            var token = tokens[i];
            var current = stack.Peek();

            if (token.Text is "{" or "(" or "[")
            {
                var kind = token.Text switch
                {
                    "{" => UniversalSyntaxKind.Block,
                    "[" => UniversalSyntaxKind.Index,
                    _ when i > 0 &&
                           tokens[i - 1].Class == TokenClass.Identifier
                        => UniversalSyntaxKind.Call,
                    _ => UniversalSyntaxKind.Group
                };

                var node = new Mutable(
                    kind,
                    token.Text,
                    token.Offset);
                current.Children.Add(node);
                stack.Push(node);
                continue;
            }

            if (token.Text is "}" or ")" or "]")
            {
                if (stack.Count > 1)
                {
                    var node = stack.Pop();
                    node.End =
                        token.Offset +
                        token.Text.Length;
                }

                continue;
            }

            if (IsAssignment(token.Text) &&
                i > 0 &&
                tokens[i - 1].Class == TokenClass.Identifier)
            {
                var assignment = new Mutable(
                    UniversalSyntaxKind.Assignment,
                    tokens[i - 1].Text,
                    tokens[i - 1].Offset)
                {
                    End =
                        token.Offset +
                        token.Text.Length
                };

                current.Children.Add(assignment);
                continue;
            }

            if (token.Text is "." or "->" or "::")
            {
                var memberAccess = new Mutable(
                    UniversalSyntaxKind.MemberAccess,
                    token.Text,
                    token.Offset)
                {
                    End =
                        token.Offset +
                        token.Text.Length
                };

                current.Children.Add(memberAccess);
                continue;
            }

            var kindForToken = token.Class switch
            {
                TokenClass.Identifier
                    => UniversalSyntaxKind.Identifier,
                TokenClass.Literal
                    => UniversalSyntaxKind.Literal,
                TokenClass.Operator
                    => UniversalSyntaxKind.Operator,
                _ => UniversalSyntaxKind.Token
            };

            current.Children.Add(
                new Mutable(
                    kindForToken,
                    token.Text,
                    token.Offset)
                {
                    End =
                        token.Offset +
                        token.Text.Length
                });
        }

        while (stack.Count > 1)
        {
            var node = stack.Pop();
            node.End = source.Length;
        }

        root.End = source.Length;
        return Freeze(root);
    }

    public static int CountNodes(UniversalSyntaxNode node)
    {
        return 1 +
               node.Children.Sum(CountNodes);
    }

    public static IEnumerable<UniversalSyntaxNode> Descendants(
        UniversalSyntaxNode node)
    {
        foreach (var child in node.Children)
        {
            yield return child;

            foreach (var nested in Descendants(child))
            {
                yield return nested;
            }
        }
    }

    private static bool IsAssignment(string token)
    {
        return token is
            "=" or
            "+=" or
            "-=" or
            "*=" or
            "/=" or
            "%=" or
            ":=";
    }

    private static UniversalSyntaxNode Freeze(Mutable node)
    {
        return new(
            node.Kind,
            node.Text,
            node.Start,
            Math.Max(
                0,
                node.End - node.Start),
            node.Children
                .Select(Freeze)
                .ToArray());
    }

    private sealed class Mutable
    {
        public Mutable(
            UniversalSyntaxKind kind,
            string text,
            int start)
        {
            Kind = kind;
            Text = text;
            Start = start;
            End = start + text.Length;
        }

        public UniversalSyntaxKind Kind;
        public string Text;
        public int Start;
        public int End;
        public List<Mutable> Children = new();
    }

    private static List<Tok> Lex(string source)
    {
        var result = new List<Tok>();

        for (var i = 0; i < source.Length;)
        {
            var current = source[i];

            if (char.IsWhiteSpace(current))
            {
                i++;
                continue;
            }

            if (current == '/' &&
                i + 1 < source.Length &&
                source[i + 1] == '/')
            {
                while (i < source.Length &&
                       source[i] != '\n')
                {
                    i++;
                }

                continue;
            }

            if (current == '#')
            {
                while (i < source.Length &&
                       source[i] != '\n')
                {
                    i++;
                }

                continue;
            }

            if (current == '/' &&
                i + 1 < source.Length &&
                source[i + 1] == '*')
            {
                i += 2;

                while (i + 1 < source.Length &&
                       !(source[i] == '*' &&
                         source[i + 1] == '/'))
                {
                    i++;
                }

                i = Math.Min(
                    source.Length,
                    i + 2);
                continue;
            }

            if (current is '"' or '\'')
            {
                var quote = current;
                var start = i++;
                var escape = false;

                while (i < source.Length)
                {
                    var value = source[i++];

                    if (escape)
                    {
                        escape = false;
                        continue;
                    }

                    if (value == '\\')
                    {
                        escape = true;
                        continue;
                    }

                    if (value == quote)
                    {
                        break;
                    }
                }

                result.Add(
                    new Tok(
                        source[start..i],
                        start,
                        TokenClass.Literal));
                continue;
            }

            if (char.IsLetter(current) ||
                current is '_' or '$')
            {
                var start = i++;

                while (i < source.Length &&
                       (char.IsLetterOrDigit(source[i]) ||
                        source[i] is '_' or '$'))
                {
                    i++;
                }

                result.Add(
                    new Tok(
                        source[start..i],
                        start,
                        TokenClass.Identifier));
                continue;
            }

            if (char.IsDigit(current))
            {
                var start = i++;

                while (i < source.Length &&
                       (char.IsLetterOrDigit(source[i]) ||
                        source[i] is '.' or '_'))
                {
                    i++;
                }

                result.Add(
                    new Tok(
                        source[start..i],
                        start,
                        TokenClass.Literal));
                continue;
            }

            var two =
                i + 1 < source.Length
                    ? source.Substring(i, 2)
                    : string.Empty;

            if (two is
                "->" or
                "::" or
                "==" or
                "!=" or
                "<=" or
                ">=" or
                "&&" or
                "||" or
                "+=" or
                "-=" or
                "*=" or
                "/=" or
                "%=" or
                ":=")
            {
                result.Add(
                    new Tok(
                        two,
                        i,
                        TokenClass.Operator));
                i += 2;
                continue;
            }

            var tokenClass =
                "=+-*/%<>!&|^?:.".Contains(current)
                    ? TokenClass.Operator
                    : TokenClass.Token;

            result.Add(
                new Tok(
                    current.ToString(),
                    i,
                    tokenClass));
            i++;
        }

        return result;
    }
}
