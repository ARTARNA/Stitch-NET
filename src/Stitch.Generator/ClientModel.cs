using System.Collections.Generic;

namespace Stitch.Generator;

internal enum HttpVerb { Get, Post, Put, Patch, Delete }

internal enum BindingSource { Route, Query, Body, Header, CancellationToken }

internal sealed record ParameterModel(
    string Name,
    string TypeName,
    BindingSource Source,
    string? HeaderName,
    string? QueryAlias,
    bool IsNullable,
    bool HasDefault);

internal sealed record MethodModel(
    string Name,
    HttpVerb Verb,
    string PathTemplate,
    string ReturnTypeName,
    bool IsVoid,
    bool IsStitchResult,
    string? StitchResultValueType,
    string? StitchResultErrorType,
    IReadOnlyList<ParameterModel> Parameters);

internal sealed record ClientModel(
    string Namespace,
    string InterfaceName,
    string ImplementationName,
    IReadOnlyList<string> Usings,
    IReadOnlyList<MethodModel> Methods);
