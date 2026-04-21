using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Stitch.Generator;

internal static class Parser
{
    private static readonly Regex RouteTokenPattern = new(@"\{(\w+)\}", RegexOptions.Compiled);

    public static ClientModel? Parse(GeneratorAttributeSyntaxContext ctx, CancellationToken ct)
    {
        if (ctx.TargetSymbol is not INamedTypeSymbol interfaceSymbol)
            return null;

        ct.ThrowIfCancellationRequested();

        var ns = interfaceSymbol.ContainingNamespace.IsGlobalNamespace
            ? string.Empty
            : interfaceSymbol.ContainingNamespace.ToDisplayString();

        var interfaceName = interfaceSymbol.Name;
        var implName = "Stitch" + (interfaceName.Length > 1 && interfaceName[0] == 'I'
            ? interfaceName.Substring(1)
            : interfaceName);

        var methods = new List<MethodModel>();
        var usings = new HashSet<string>(StringComparer.Ordinal);

        foreach (var member in interfaceSymbol.GetMembers())
        {
            if (member is not IMethodSymbol method)
                continue;

            ct.ThrowIfCancellationRequested();

            var verbAttr = GetHttpVerbAttribute(method);
            if (verbAttr == null)
                continue;

            var (verb, path) = verbAttr.Value;
            var parameters = ParseParameters(method, path, usings);
            var (returnType, isVoid, isStitchResult, valueType, errorType) =
                ParseReturnType(method, usings);

            methods.Add(new MethodModel(
                method.Name,
                verb,
                path,
                returnType,
                isVoid,
                isStitchResult,
                valueType,
                errorType,
                parameters));
        }

        return new ClientModel(ns, interfaceName, implName, usings.OrderBy(u => u).ToList(), methods);
    }

    private static (HttpVerb Verb, string Path)? GetHttpVerbAttribute(IMethodSymbol method)
    {
        foreach (var attr in method.GetAttributes())
        {
            var name = attr.AttributeClass?.Name;
            var path = attr.ConstructorArguments.Length > 0
                ? attr.ConstructorArguments[0].Value as string ?? "/"
                : "/";

            if (name == "GetAttribute") return (HttpVerb.Get, path);
            if (name == "PostAttribute") return (HttpVerb.Post, path);
            if (name == "PutAttribute") return (HttpVerb.Put, path);
            if (name == "PatchAttribute") return (HttpVerb.Patch, path);
            if (name == "DeleteAttribute") return (HttpVerb.Delete, path);
        }
        return null;
    }

    private static IReadOnlyList<ParameterModel> ParseParameters(
        IMethodSymbol method,
        string path,
        HashSet<string> usings)
    {
        var routeTokens = new HashSet<string>(
            RouteTokenPattern.Matches(path).Cast<Match>().Select(m => m.Groups[1].Value),
            StringComparer.OrdinalIgnoreCase);

        var result = new List<ParameterModel>();

        foreach (var param in method.Parameters)
        {
            var typeName = GetTypeName(param.Type, usings);
            var source = DetermineBinding(param, routeTokens, method);

            string? headerName = null;
            string? queryAlias = null;

            if (source == BindingSource.Header)
            {
                var headerAttr = param.GetAttributes()
                    .FirstOrDefault(a => a.AttributeClass?.Name == "HeaderAttribute");
                headerName = headerAttr?.ConstructorArguments[0].Value as string;
            }
            else if (source == BindingSource.Query)
            {
                var queryAttr = param.GetAttributes()
                    .FirstOrDefault(a => a.AttributeClass?.Name == "QueryAttribute");
                queryAlias = queryAttr?.ConstructorArguments.Length > 0
                    ? queryAttr.ConstructorArguments[0].Value as string
                    : null;
            }

            result.Add(new ParameterModel(
                param.Name,
                typeName,
                source,
                headerName,
                queryAlias,
                param.Type.NullableAnnotation == NullableAnnotation.Annotated,
                param.HasExplicitDefaultValue));
        }

        return result;
    }

    private static BindingSource DetermineBinding(
        IParameterSymbol param,
        HashSet<string> routeTokens,
        IMethodSymbol method)
    {
        var typeName = param.Type.OriginalDefinition.ToDisplayString();
        if (typeName == "System.Threading.CancellationToken")
            return BindingSource.CancellationToken;

        foreach (var attr in param.GetAttributes())
        {
            var name = attr.AttributeClass?.Name;
            if (name == "BodyAttribute") return BindingSource.Body;
            if (name == "QueryAttribute") return BindingSource.Query;
            if (name == "HeaderAttribute") return BindingSource.Header;
        }

        if (routeTokens.Contains(param.Name))
            return BindingSource.Route;

        var isComplex = param.Type.TypeKind == TypeKind.Class
            || param.Type.TypeKind == TypeKind.Interface
            || param.Type.TypeKind == TypeKind.Struct && !IsSimpleValueType(param.Type);

        if (isComplex && (method.GetAttributes().Any(a =>
            a.AttributeClass?.Name is "PostAttribute" or "PutAttribute" or "PatchAttribute")))
            return BindingSource.Body;

        return BindingSource.Query;
    }

    private static bool IsSimpleValueType(ITypeSymbol type)
    {
        return type.SpecialType
            is SpecialType.System_Boolean
            or SpecialType.System_Byte
            or SpecialType.System_SByte
            or SpecialType.System_Int16
            or SpecialType.System_UInt16
            or SpecialType.System_Int32
            or SpecialType.System_UInt32
            or SpecialType.System_Int64
            or SpecialType.System_UInt64
            or SpecialType.System_Single
            or SpecialType.System_Double
            or SpecialType.System_Decimal
            or SpecialType.System_Char
            or SpecialType.System_String
            || type.Name == "Guid"
            || type.Name == "DateTime"
            || type.Name == "DateTimeOffset"
            || type.Name == "TimeSpan"
            || type.Name == "DateOnly"
            || type.Name == "TimeOnly";
    }

    private static (string returnType, bool isVoid, bool isStitchResult,
        string? valueType, string? errorType) ParseReturnType(
        IMethodSymbol method,
        HashSet<string> usings)
    {
        var returnType = method.ReturnType;

        if (returnType is not INamedTypeSymbol namedReturn)
            return (GetTypeName(returnType, usings), true, false, null, null);

        // Unwrap Task<T> or ValueTask<T>
        var outerName = namedReturn.OriginalDefinition.ToDisplayString();
        if (outerName is "System.Threading.Tasks.Task"
            or "System.Threading.Tasks.ValueTask")
            return ("void_task", true, false, null, null);

        if (outerName is not ("System.Threading.Tasks.Task<TResult>"
            or "System.Threading.Tasks.ValueTask<TResult>"))
            return (GetTypeName(returnType, usings), false, false, null, null);

        var innerType = namedReturn.TypeArguments[0];
        if (innerType is INamedTypeSymbol innerNamed
            && innerNamed.OriginalDefinition.ToDisplayString() == "Stitch.Core.StitchResult<TValue, TError>")
        {
            var vt = GetTypeName(innerNamed.TypeArguments[0], usings);
            var et = GetTypeName(innerNamed.TypeArguments[1], usings);
            var fullReturn = GetTypeName(returnType, usings);
            return (fullReturn, false, true, vt, et);
        }

        return (GetTypeName(returnType, usings), false, false, null, null);
    }

    private static string GetTypeName(ITypeSymbol type, HashSet<string> usings)
    {
        if (type is INamedTypeSymbol named && !named.IsGenericType)
        {
            var ns = named.ContainingNamespace?.ToDisplayString();
            if (!string.IsNullOrEmpty(ns) && ns != "System")
                usings.Add(ns!);
        }

        return type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);
    }
}
