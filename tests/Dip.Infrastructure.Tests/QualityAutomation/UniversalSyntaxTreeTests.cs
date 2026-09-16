// Purpose: Verifies U ni ve rs al Sy nt ax Tr ee Te st s behavior.
using Dip.Infrastructure.QualityAutomation;
namespace Dip.Infrastructure.Tests.QualityAutomation;
public sealed class UniversalSyntaxTreeTests
{
    [Fact] public void Parse_recognizes_calls_and_assignments(){var tree=UniversalSyntaxTree.Parse("var input = Request.Query[\"x\"]; Process.Start(input);");var nodes=Flatten(tree).ToArray();Assert.Contains(nodes,x=>x.Kind==UniversalSyntaxKind.Assignment&&x.Text=="input");Assert.Contains(nodes,x=>x.Kind==UniversalSyntaxKind.Call);}
    [Fact] public void Parse_ignores_comment_braces(){var tree=UniversalSyntaxTree.Parse("// { fake }\nif(x){y();}");Assert.True(UniversalSyntaxTree.CountNodes(tree)>1);}
    private static IEnumerable<UniversalSyntaxNode> Flatten(UniversalSyntaxNode n){yield return n;foreach(var c in n.Children)foreach(var x in Flatten(c))yield return x;}
}
