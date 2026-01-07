using System.Collections.Generic;
using Microsoft.CodeAnalysis;

namespace BlazorBridge.SourceGenerator;

record struct GeneratedProxy
{
    public INamedTypeSymbol InterfaceSymbol { get; set; }
    public string? ModulePath { get; set; }
    public string? ExportedObjectPrefix { get; set; }
    public List<GeneratedMethod>? Methods { get; set; }

    public string ImplementationTypeName => InterfaceSymbol.Name.TrimStart('I');
}