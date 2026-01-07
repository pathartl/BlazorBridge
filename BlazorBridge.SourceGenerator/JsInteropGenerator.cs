using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;


namespace BlazorBridge.SourceGenerator;

/// <summary>
/// A sample source generator that creates a custom report based on class properties. The target class should be annotated with the 'Generators.ReportAttribute' attribute.
/// When using the source code as a baseline, an incremental source generator is preferable because it reduces the performance overhead.
/// </summary>
[Generator]
public sealed class JsInteropGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var interfaceDeclarations = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (node, _) => node is InterfaceDeclarationSyntax,
                transform: static (ctx, _) =>
                {
                    var interfaceSyntax = (InterfaceDeclarationSyntax)ctx.Node;
                    return ctx.SemanticModel.GetDeclaredSymbol(interfaceSyntax) as INamedTypeSymbol;
                })
            .Where(static symbol => symbol is not null)!
            .Select(static (symbol, _) => symbol!);
        
        var compilationAndInterfaces = context.CompilationProvider
            .Combine(interfaceDeclarations.Collect());
        
        context.RegisterSourceOutput(compilationAndInterfaces, Execute);
    }

    private static void Execute(SourceProductionContext context, (Compilation Compilation, ImmutableArray<INamedTypeSymbol> Interfaces) input)
    {
        var (compilation, interfaces) = input;

        if (interfaces.IsDefaultOrEmpty)
            return;
        
        var moduleAttributeSymbol = compilation.GetTypeByMetadataName("BlazorBridge.JsModuleAttribute");
        var exportAttributeSymbol = compilation.GetTypeByMetadataName("BlazorBridge.JsExportAttribute");
        var defaultExportAttributeSymbol = compilation.GetTypeByMetadataName("BlazorBridge.JsDefaultExportAttribute");
        var methodAttributeSymbol = compilation.GetTypeByMetadataName("BlazorBridge.JsMethodAttribute");
        var interopSymbol = compilation.GetTypeByMetadataName("BlazorBridge.IJsInterop");

        if (moduleAttributeSymbol is null || interopSymbol is null)
            return;

        var proxies = new List<GeneratedProxy>();

        foreach (var @interface in interfaces)
        {
            if (!ImplementsInterface(@interface, interopSymbol))
                continue;
            
            var moduleAttribute = GetAttribute(@interface, moduleAttributeSymbol);

            if (moduleAttribute is null)
                continue;

            var modulePath = moduleAttribute.ConstructorArguments[0].Value as string ?? string.Empty;
            
            var exportAttribute = exportAttributeSymbol is null ? null : GetAttribute(@interface, exportAttributeSymbol);
            var defaultExportAttribute = defaultExportAttributeSymbol is null ? null : GetAttribute(@interface, defaultExportAttributeSymbol);

            var exportedObjectPrefix = string.Empty;

            if (defaultExportAttribute is not null)
                exportedObjectPrefix = "default";
            else if (exportAttribute is not null)
                exportedObjectPrefix = exportAttribute.ConstructorArguments[0].Value as string ?? string.Empty;
            
            var methods = CollectMethods(@interface, methodAttributeSymbol);

            if (methods.Count == 0)
                continue;

            var proxy = new GeneratedProxy
            {
                InterfaceSymbol = @interface,
                ModulePath = modulePath,
                ExportedObjectPrefix = exportedObjectPrefix,
                Methods = methods
            };

            proxies.Add(proxy);
        }
        
        if (!proxies.Any())
            return;

        foreach (var proxy in proxies)
        {
            var source = GenerateProxyClass(proxy);
                
            context.AddSource($"{proxy.ImplementationTypeName}.g.cs", SourceText.From(source, Encoding.UTF8));
        }
        
        var dependencyInjectionSource = GenerateDependencyInjectionRegistration(proxies);
        
        context.AddSource("JsInteropServiceCollectionExtensions.g.cs", SourceText.From(dependencyInjectionSource, Encoding.UTF8));
    }

    private static bool ImplementsInterface(INamedTypeSymbol symbol, INamedTypeSymbol interfaceSymbol)
    {
        foreach (var i in symbol.AllInterfaces)
            if (SymbolEqualityComparer.Default.Equals(i, interfaceSymbol))
                return true;

        return false;
    }

    private static AttributeData? GetAttribute(ISymbol symbol, INamedTypeSymbol attributeSymbol)
    {
        foreach (var attribute in symbol.GetAttributes())
            if (SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, attributeSymbol))
                return attribute;

        return null;
    }

    private static List<GeneratedMethod> CollectMethods(INamedTypeSymbol interfaceSymbol,
        INamedTypeSymbol? methodAttributeSymbol)
    {
        var result = new List<GeneratedMethod>();

        foreach (var member in interfaceSymbol.GetMembers().OfType<IMethodSymbol>())
        {
            if (member.MethodKind != MethodKind.Ordinary)
                continue;

            // Don't generate for overloads
            if (result.Any(m => m.InterfaceMethodName == member.Name))
                continue;

            var methodName = member.Name;
            
            if (methodAttributeSymbol is not null)
            {
                var methodAttribute = GetAttribute(member, methodAttributeSymbol);

                if (methodAttribute is not null)
                {
                    var name = methodAttribute.ConstructorArguments[0].Value as string;
                    
                    if (!string.IsNullOrWhiteSpace(name))
                        methodName = name;
                }
            }

            var returnType = member.ReturnType;
            var isValueTask = returnType.Name == "ValueTask";

            if (!isValueTask)
                continue;

            ITypeSymbol? typeArgument = null;
            
            if (returnType is INamedTypeSymbol namedSymbol && namedSymbol.IsGenericType)
                typeArgument = namedSymbol.TypeArguments[0];

            var parameters = member.Parameters.Select(p => new ParameterInfo
            {
                TypeName = p.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                Name = p.Name
            }).ToImmutableArray();
            
            result.Add(new GeneratedMethod
            {
                InterfaceMethodName = member.Name,
                JsMethodName = methodName,
                ReturnType = returnType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                ReturnTypeArgument = typeArgument?.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                Parameters = parameters
            });
        }

        return result;
    }

    private static string GenerateProxyClass(GeneratedProxy proxy)
    {
        var interfaceSymbol = proxy.InterfaceSymbol;
        var interfaceName = interfaceSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        var ns = interfaceSymbol.ContainingNamespace.IsGlobalNamespace
            ? "GlobalNamespace"
            : interfaceSymbol.ContainingNamespace.ToDisplayString();

        var implementationName = proxy.ImplementationTypeName;

        var builder = new StringBuilder();

        builder.AppendLine("// <auto-generated />");
        builder.AppendLine("using System;");
        builder.AppendLine("using System.Threading.Tasks;");
        builder.AppendLine("using Microsoft.JSInterop;");
        builder.AppendLine();
        builder.Append("namespace ").AppendLine(ns);
        builder.AppendLine("{");
        builder.AppendLine($"    internal sealed class {implementationName} : {interfaceName}, IAsyncDisposable");
        builder.AppendLine("    {");
        builder.AppendLine("        private readonly IJSRuntime _jsRuntime;");
        builder.AppendLine("        private IJSObjectReference? _module;");
        builder.AppendLine();
        builder.AppendLine($"        public {implementationName}(IJSRuntime jsRuntime)");
        builder.AppendLine("        {");
        builder.AppendLine("            _jsRuntime = jsRuntime ?? throw new ArgumentNullException(nameof(jsRuntime));");
        builder.AppendLine("        }");
        builder.AppendLine();
        builder.AppendLine("        private async ValueTask<IJSObjectReference> GetModuleAsync()");
        builder.AppendLine("        {");
        builder.AppendLine("            if (_module is not null)");
        builder.AppendLine("                return _module;");
        builder.AppendLine();
        builder.AppendLine("            _module = await _jsRuntime.InvokeAsync<IJSObjectReference>(\"import\", ");
        builder.Append("@\"").Append(proxy.ModulePath.Replace("\"", "\"\"")).AppendLine("\");");
        builder.AppendLine("            return _module;");
        builder.AppendLine("        }");
        builder.AppendLine();

        foreach (var method in proxy.Methods)
        {
            builder.Append(GenerateMethod(proxy, method));
            builder.AppendLine();
        }
        
        // DisposeAsync
        builder.AppendLine("        public async ValueTask DisposeAsync()");
        builder.AppendLine("        {");
        builder.AppendLine("            if (_module is not null)");
        builder.AppendLine("            {");
        builder.AppendLine("                try");
        builder.AppendLine("                {");
        builder.AppendLine("                    await _module.DisposeAsync();");
        builder.AppendLine("                }");
        builder.AppendLine("                catch (JSDisconnectedException)");
        builder.AppendLine("                {");
        builder.AppendLine("                    // Ignore errors during teardown");
        builder.AppendLine("                }");
        builder.AppendLine("            }");
        builder.AppendLine("        }");

        builder.AppendLine("    }");
        builder.AppendLine("}");

        return builder.ToString();
    }
    
    private static string GenerateMethod(GeneratedProxy proxy, GeneratedMethod method)
    {
        var builder = new StringBuilder();

        var hasReturn = method.ReturnTypeArgument is not null;
        var methodParameters = string.Join(", ", method.Parameters.Select(p => $"{p.TypeName} {p.Name}"));

        // Signature
        builder.AppendLine($"        public async {method.ReturnType} {method.InterfaceMethodName}({methodParameters})");
        builder.AppendLine("        {");
        builder.AppendLine("            var module = await GetModuleAsync();");

        var jsIdentifier = method.JsMethodName;

        // Build the JS path: ExportedObjectPrefix.methodName or just methodName
        var jsPath = string.IsNullOrWhiteSpace(proxy.ExportedObjectPrefix)
            ? jsIdentifier
            : $"{proxy.ExportedObjectPrefix}.{jsIdentifier}";
        
        var moduleImportParameters = string.Join(", ", method.Parameters.Select(p => p.Name));

        if (hasReturn)
            builder.AppendLine($"            return await module.InvokeAsync<{method.ReturnTypeArgument}>(\"{jsPath}\", {moduleImportParameters});");
        else
            builder.AppendLine($"            await module.InvokeVoidAsync(\"{jsPath}\", {moduleImportParameters});");

        builder.AppendLine("        }");

        return builder.ToString();
    }
    
    private static string GenerateDependencyInjectionRegistration(IReadOnlyList<GeneratedProxy> proxies)
    {
        var namespaces = proxies
            .Select(p => p.InterfaceSymbol.ContainingNamespace.ToDisplayString())
            .Where(ns => ns != "GlobalNamespace")
            .Distinct();
        
        var builder = new StringBuilder();
        
        builder.AppendLine("// <auto-generated />");
        builder.AppendLine("using Microsoft.Extensions.DependencyInjection;");

        foreach (var ns in namespaces)
            builder.AppendLine($"using {ns};");
        
        builder.AppendLine();
        builder.AppendLine("namespace BlazorBridge");
        builder.AppendLine("{");
        builder.AppendLine("    public static class JsInteropServiceCollectionExtensions");
        builder.AppendLine("    {");
        builder.AppendLine("        public static IServiceCollection AddJsInterops(this IServiceCollection services)");
        builder.AppendLine("        {");

        foreach (var proxy in proxies)
        {
            var interfaceName = proxy.InterfaceSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            var implementationName = $"{proxy.InterfaceSymbol.ContainingNamespace.ToDisplayString()}.{proxy.ImplementationTypeName}";
            
            builder.AppendLine($"            services.AddScoped<{interfaceName}, {implementationName}>();");
        }

        builder.AppendLine("            return services;");
        builder.AppendLine("        }");
        builder.AppendLine("    }");
        builder.AppendLine("}");

        return builder.ToString();
    }
}