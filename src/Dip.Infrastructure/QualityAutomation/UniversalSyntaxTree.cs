namespace Dip.Infrastructure.QualityAutomation;

public enum UniversalSyntaxKind{Root,Block,Group,Index,Call,Assignment,Identifier,Literal,Token}
public sealed record UniversalSyntaxNode(UniversalSyntaxKind Kind,string Text,int Offset,int Length,IReadOnlyList<UniversalSyntaxNode> Children);

/// <summary>
/// Small dependency-free syntax tree used as the universal fallback when no
/// language-native AST is available. It recognizes balanced blocks/groups,
/// calls and assignments while preserving offsets. Language-specific analyzers
/// remain higher-confidence evidence.
/// </summary>
public static class UniversalSyntaxTree
{
    private sealed record Tok(string Text,int Offset,bool Identifier,bool Literal);
    public static UniversalSyntaxNode Parse(string source)
    {
        var tokens=Lex(source);var root=new Mutable(UniversalSyntaxKind.Root,"root",0);var stack=new Stack<Mutable>();stack.Push(root);
        for(var i=0;i<tokens.Count;i++)
        {
            var t=tokens[i];var current=stack.Peek();
            if(t.Text is "{" or "(" or "[")
            {
                var kind=t.Text=="{"?UniversalSyntaxKind.Block:t.Text=="["?UniversalSyntaxKind.Index:UniversalSyntaxKind.Group;
                if(t.Text=="("&&i>0&&tokens[i-1].Identifier)kind=UniversalSyntaxKind.Call;
                var node=new Mutable(kind,t.Text,t.Offset);current.Children.Add(node);stack.Push(node);continue;
            }
            if(t.Text is "}" or ")" or "]"){if(stack.Count>1){var n=stack.Pop();n.End=t.Offset+t.Text.Length;}continue;}
            if(t.Text=="="&&i>0&&tokens[i-1].Identifier){var node=new Mutable(UniversalSyntaxKind.Assignment,tokens[i-1].Text,tokens[i-1].Offset){End=t.Offset+1};current.Children.Add(node);continue;}
            current.Children.Add(new Mutable(t.Identifier?UniversalSyntaxKind.Identifier:t.Literal?UniversalSyntaxKind.Literal:UniversalSyntaxKind.Token,t.Text,t.Offset){End=t.Offset+t.Text.Length});
        }
        while(stack.Count>1){var n=stack.Pop();n.End=source.Length;}root.End=source.Length;return Freeze(root);
    }
    public static int CountNodes(UniversalSyntaxNode node)=>1+node.Children.Sum(CountNodes);
    private static UniversalSyntaxNode Freeze(Mutable n)=>new(n.Kind,n.Text,n.Start,Math.Max(0,n.End-n.Start),n.Children.Select(Freeze).ToArray());
    private sealed class Mutable(UniversalSyntaxKind kind,string text,int start){public UniversalSyntaxKind Kind=kind;public string Text=text;public int Start=start;public int End=start+text.Length;public List<Mutable> Children=new();}
    private static List<Tok> Lex(string s)
    {
        var r=new List<Tok>();for(var i=0;i<s.Length;)
        {
            var c=s[i];if(char.IsWhiteSpace(c)){i++;continue;}if(c=='/'&&i+1<s.Length&&s[i+1]=='/'){while(i<s.Length&&s[i]!='\n')i++;continue;}if(c=='#'){while(i<s.Length&&s[i]!='\n')i++;continue;}if(c=='/'&&i+1<s.Length&&s[i+1]=='*'){i+=2;while(i+1<s.Length&&!(s[i]=='*'&&s[i+1]=='/'))i++;i=Math.Min(s.Length,i+2);continue;}
            if(c is '"' or '\''){var q=c;var start=i++;var escape=false;while(i<s.Length){var x=s[i++];if(escape){escape=false;continue;}if(x=='\\'){escape=true;continue;}if(x==q)break;}r.Add(new(s[start..i],start,false,true));continue;}
            if(char.IsLetter(c)||c=='_'||c=='$'){var start=i++;while(i<s.Length&&(char.IsLetterOrDigit(s[i])||s[i] is '_' or '$'))i++;r.Add(new(s[start..i],start,true,false));continue;}
            if(char.IsDigit(c)){var start=i++;while(i<s.Length&&(char.IsLetterOrDigit(s[i])||s[i] is '.' or '_'))i++;r.Add(new(s[start..i],start,false,true));continue;}r.Add(new(c.ToString(),i,false,false));i++;
        }return r;
    }
}
