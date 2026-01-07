using System.Collections.Immutable;

namespace BlazorBridge.SourceGenerator;

public record GeneratedMethod
{
    public string? InterfaceMethodName { get; set; }
    public string? JsMethodName { get; set; }
    public string? ReturnType { get; set; }
    public string? ReturnTypeArgument { get; set; }
    public ImmutableArray<ParameterInfo> Parameters { get; set; }
}