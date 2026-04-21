using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Stitch.Generator;

[Generator]
public sealed class StitchGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var interfaces = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                "Stitch.Core.StitchClientAttribute",
                predicate: static (node, _) => node is InterfaceDeclarationSyntax,
                transform: static (ctx, ct) => Parser.Parse(ctx, ct))
            .Where(static m => m is not null);

        context.RegisterSourceOutput(
            interfaces,
            static (spc, model) => Emitter.Emit(spc, model!));
    }
}
