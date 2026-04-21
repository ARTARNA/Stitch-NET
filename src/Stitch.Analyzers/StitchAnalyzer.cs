using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Stitch.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class StitchAnalyzer : DiagnosticAnalyzer
{
    private static readonly Regex RouteToken = new(@"\{(\w+)\}", RegexOptions.Compiled);

    private static readonly ImmutableArray<string> HttpVerbAttributes =
        ImmutableArray.Create("GetAttribute", "PostAttribute", "PutAttribute", "PatchAttribute", "DeleteAttribute");

    private static readonly ImmutableArray<string> BodyUnsafeVerbs =
        ImmutableArray.Create("GetAttribute", "DeleteAttribute");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(
            Diagnostics.ST001_UnmatchedRouteToken,
            Diagnostics.ST002_UnusedParameterLooksLikeRoute,
            Diagnostics.ST003_BodyOnGetOrDelete,
            Diagnostics.ST004_MultipleBodyParameters,
            Diagnostics.ST005_InvalidReturnType,
            Diagnostics.ST006_NotAnInterface,
            Diagnostics.ST007_MissingHttpVerb);

    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterSymbolAction(AnalyzeNamedType, SymbolKind.NamedType);
    }

    private static void AnalyzeNamedType(SymbolAnalysisContext ctx)
    {
        var type = (INamedTypeSymbol)ctx.Symbol;

        var stitchAttr = type.GetAttributes()
            .FirstOrDefault(a => a.AttributeClass?.Name == "StitchClientAttribute");

        if (stitchAttr == null)
            return;

        if (type.TypeKind != TypeKind.Interface)
        {
            ctx.ReportDiagnostic(Diagnostic.Create(
                Diagnostics.ST006_NotAnInterface,
                stitchAttr.ApplicationSyntaxReference?.GetSyntax().GetLocation(),
                type.Name));
            return;
        }

        foreach (var member in type.GetMembers())
        {
            if (member is not IMethodSymbol method)
                continue;

            AnalyzeMethod(ctx, method);
        }
    }

    private static void AnalyzeMethod(SymbolAnalysisContext ctx, IMethodSymbol method)
    {
        var location = method.Locations.FirstOrDefault();

        ValidateReturnType(ctx, method, location);

        var verbAttr = method.GetAttributes()
            .FirstOrDefault(a => HttpVerbAttributes.Contains(a.AttributeClass?.Name!));

        if (verbAttr == null)
        {
            ctx.ReportDiagnostic(Diagnostic.Create(
                Diagnostics.ST007_MissingHttpVerb, location, method.Name));
            return;
        }

        var path = verbAttr.ConstructorArguments.Length > 0
            ? verbAttr.ConstructorArguments[0].Value as string ?? "/"
            : "/";

        var routeTokens = RouteToken.Matches(path)
            .Cast<Match>()
            .Select(m => m.Groups[1].Value)
            .ToList();

        var parameterNames = method.Parameters
            .Where(p => p.Type.OriginalDefinition.ToDisplayString() != "System.Threading.CancellationToken")
            .Select(p => p.Name)
            .ToList();

        ValidateRouteTokens(ctx, method, routeTokens, parameterNames, location);
        ValidateBodyParameters(ctx, method, verbAttr, location);
    }

    private static void ValidateReturnType(
        SymbolAnalysisContext ctx, IMethodSymbol method, Location? location)
    {
        if (method.ReturnType is not INamedTypeSymbol returnType)
        {
            ctx.ReportDiagnostic(Diagnostic.Create(
                Diagnostics.ST005_InvalidReturnType, location,
                method.ReturnType.ToDisplayString()));
            return;
        }

        var outer = returnType.OriginalDefinition.ToDisplayString();
        if (outer is "System.Threading.Tasks.Task"
            or "System.Threading.Tasks.Task<TResult>"
            or "System.Threading.Tasks.ValueTask<TResult>")
            return;

        ctx.ReportDiagnostic(Diagnostic.Create(
            Diagnostics.ST005_InvalidReturnType, location,
            returnType.ToDisplayString()));
    }

    private static void ValidateRouteTokens(
        SymbolAnalysisContext ctx,
        IMethodSymbol method,
        List<string> routeTokens,
        List<string> parameterNames,
        Location? methodLocation)
    {
        foreach (var token in routeTokens)
        {
            var matched = parameterNames.Any(n =>
                string.Equals(n, token, StringComparison.OrdinalIgnoreCase));

            if (!matched)
            {
                ctx.ReportDiagnostic(Diagnostic.Create(
                    Diagnostics.ST001_UnmatchedRouteToken, methodLocation, token, method.Name));
            }
        }

        foreach (var param in method.Parameters)
        {
            if (param.Type.OriginalDefinition.ToDisplayString() == "System.Threading.CancellationToken")
                continue;

            var hasRouteToken = routeTokens.Any(t =>
                string.Equals(t, param.Name, StringComparison.OrdinalIgnoreCase));
            if (hasRouteToken)
                continue;

            var hasExplicitBinding = param.GetAttributes().Any(a =>
                a.AttributeClass?.Name is "BodyAttribute" or "QueryAttribute" or "HeaderAttribute");
            if (hasExplicitBinding)
                continue;

            var closestToken = routeTokens
                .Select(t => (Token: t, Score: LevenshteinDistance(param.Name, t)))
                .Where(x => x.Score <= 2 && x.Score > 0)
                .OrderBy(x => x.Score)
                .Select(x => x.Token)
                .FirstOrDefault();

            if (closestToken != null)
            {
                var paramLocation = param.Locations.FirstOrDefault() ?? methodLocation;
                ctx.ReportDiagnostic(Diagnostic.Create(
                    Diagnostics.ST002_UnusedParameterLooksLikeRoute,
                    paramLocation,
                    additionalLocations: ImmutableArray<Location>.Empty,
                    properties: ImmutableDictionary<string, string?>.Empty
                        .Add("routeToken", closestToken),
                    param.Name,
                    closestToken));
            }
        }
    }

    private static void ValidateBodyParameters(
        SymbolAnalysisContext ctx,
        IMethodSymbol method,
        AttributeData verbAttr,
        Location? location)
    {
        var bodyParams = method.Parameters
            .Where(p => p.GetAttributes().Any(a => a.AttributeClass?.Name == "BodyAttribute"))
            .ToList();

        if (bodyParams.Count == 0)
            return;

        if (BodyUnsafeVerbs.Contains(verbAttr.AttributeClass?.Name!))
        {
            var verbName = verbAttr.AttributeClass!.Name.Replace("Attribute", "").ToUpperInvariant();
            foreach (var bp in bodyParams)
            {
                var paramLocation = bp.Locations.FirstOrDefault() ?? location;
                ctx.ReportDiagnostic(Diagnostic.Create(
                    Diagnostics.ST003_BodyOnGetOrDelete, paramLocation, verbName));
            }
        }

        if (bodyParams.Count > 1)
        {
            ctx.ReportDiagnostic(Diagnostic.Create(
                Diagnostics.ST004_MultipleBodyParameters, location, method.Name));
        }
    }

    private static int LevenshteinDistance(string a, string b)
    {
        a = a.ToLowerInvariant();
        b = b.ToLowerInvariant();

        var dp = new int[a.Length + 1, b.Length + 1];
        for (var i = 0; i <= a.Length; i++) dp[i, 0] = i;
        for (var j = 0; j <= b.Length; j++) dp[0, j] = j;

        for (var i = 1; i <= a.Length; i++)
        {
            for (var j = 1; j <= b.Length; j++)
            {
                dp[i, j] = a[i - 1] == b[j - 1]
                    ? dp[i - 1, j - 1]
                    : 1 + Math.Min(dp[i - 1, j - 1], Math.Min(dp[i - 1, j], dp[i, j - 1]));
            }
        }

        return dp[a.Length, b.Length];
    }
}
