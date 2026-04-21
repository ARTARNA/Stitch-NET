using Microsoft.CodeAnalysis;

namespace Stitch.Analyzers;

internal static class Diagnostics
{
    private const string Category = "Stitch";

    public static readonly DiagnosticDescriptor ST001_UnmatchedRouteToken = new(
        id: "ST001",
        title: "Unmatched route token",
        messageFormat: "Route token '{{{0}}}' has no matching parameter on method '{1}'",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Every token in the route template must correspond to a method parameter.");

    public static readonly DiagnosticDescriptor ST002_UnusedParameterLooksLikeRoute = new(
        id: "ST002",
        title: "Parameter name resembles a route token",
        messageFormat: "Parameter '{0}' matches no route token — did you mean '{{{1}}}'?",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "A parameter name closely matches a route token. Rename the parameter or add a matching token to the route template.");

    public static readonly DiagnosticDescriptor ST003_BodyOnGetOrDelete = new(
        id: "ST003",
        title: "[Body] on GET or DELETE method",
        messageFormat: "[Body] is not valid on {0} methods",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "HTTP GET and DELETE methods cannot carry a request body.");

    public static readonly DiagnosticDescriptor ST004_MultipleBodyParameters = new(
        id: "ST004",
        title: "Multiple [Body] parameters",
        messageFormat: "Method '{0}' has more than one [Body] parameter",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Only one parameter per method may be marked [Body].");

    public static readonly DiagnosticDescriptor ST005_InvalidReturnType = new(
        id: "ST005",
        title: "Invalid return type",
        messageFormat: "'{0}' is not a valid return type — must be Task, Task<T>, or ValueTask<T>",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "All interface methods on a [StitchClient] must return Task, Task<T>, or ValueTask<T>.");

    public static readonly DiagnosticDescriptor ST006_NotAnInterface = new(
        id: "ST006",
        title: "[StitchClient] on non-interface",
        messageFormat: "[StitchClient] can only be applied to interfaces, not to '{0}'",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "[StitchClient] is only valid on interface declarations.");

    public static readonly DiagnosticDescriptor ST007_MissingHttpVerb = new(
        id: "ST007",
        title: "No HTTP verb attribute",
        messageFormat: "Method '{0}' has no HTTP verb attribute ([Get], [Post], [Put], [Patch], or [Delete])",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Every method on a [StitchClient] interface must declare exactly one HTTP verb attribute.");
}
