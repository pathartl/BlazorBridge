namespace BlazorBridge;

/// <summary>
/// Marks an interface as a JS interop and specifies the module path.
/// Example: [JsModule("./js/myModule.js")]
/// </summary>
[AttributeUsage(AttributeTargets.Interface, AllowMultiple = false)]
public sealed class JsModuleAttribute : Attribute
{
    public string ModulePath { get; }
    
    public JsModuleAttribute(string modulePath)
        => ModulePath = modulePath;
}