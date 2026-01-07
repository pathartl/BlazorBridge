namespace BlazorBridge;

/// <summary>
/// Maps a C# method to a JS function name.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public class JsMethodAttribute : Attribute
{
    public JsMethodAttribute(string name)
        => Name = name;
    
    public string Name { get; }
}