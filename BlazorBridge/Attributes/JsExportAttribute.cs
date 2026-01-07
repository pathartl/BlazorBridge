namespace BlazorBridge;

/// <summary>
/// Defines the name of the exported object in the module.
/// Example:
/// export const myObject = { ... };
/// [JsExport("myObject")]
/// </summary>
[AttributeUsage(AttributeTargets.Interface, AllowMultiple = false)]
public sealed class JsExportAttribute : Attribute
{
    public string ExportName { get; }

    public JsExportAttribute(string exportName)
        => ExportName = exportName;
}