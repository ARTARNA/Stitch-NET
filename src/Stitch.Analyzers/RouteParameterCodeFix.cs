using System.Collections.Immutable;
using System.Composition;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Rename;

namespace Stitch.Analyzers;

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(RouteParameterCodeFix))]
[Shared]
public sealed class RouteParameterCodeFix : CodeFixProvider
{
    public override ImmutableArray<string> FixableDiagnosticIds =>
        ImmutableArray.Create("ST002");

    public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var diagnostic = context.Diagnostics[0];

        if (!diagnostic.Properties.TryGetValue("routeToken", out var routeToken) || routeToken == null)
            return;

        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken);
        if (root == null) return;

        var node = root.FindNode(diagnostic.Location.SourceSpan);
        var paramSyntax = node.FirstAncestorOrSelf<ParameterSyntax>();
        if (paramSyntax == null) return;

        var semanticModel = await context.Document.GetSemanticModelAsync(context.CancellationToken);
        if (semanticModel == null) return;

        var paramSymbol = semanticModel.GetDeclaredSymbol(paramSyntax, context.CancellationToken);
        if (paramSymbol == null) return;

        context.RegisterCodeFix(
            CodeAction.Create(
                title: $"Rename parameter to '{routeToken}'",
                createChangedSolution: ct => RenameParameterAsync(
                    context.Document.Project.Solution,
                    paramSymbol,
                    routeToken,
                    ct),
                equivalenceKey: $"ST002_Rename_{routeToken}"),
            diagnostic);
    }

    private static async Task<Solution> RenameParameterAsync(
        Solution solution,
        ISymbol paramSymbol,
        string newName,
        CancellationToken ct)
    {
        return await Renamer.RenameSymbolAsync(
            solution, paramSymbol, new SymbolRenameOptions(), newName, ct);
    }
}
