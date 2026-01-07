namespace BlazorBridge;

/// <summary>
/// Indicates that the interface is bound to the module's default export.
/// Example:
/// export default { focus() { ... }, blur() { ... };
/// [JsDefaultExport]
/// </summary>
public sealed class JsDefaultExportAttribute : Attribute
{
}