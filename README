# BlazorBridge

**BlazorBridge** is a source-generator-driven library that creates **strongly-typed JavaScript interop bindings** in Blazor from simple C# interface declarations.

Instead of writing `IJSRuntime.InvokeAsync` calls manually, you define an interface with attributes that describe the JavaScript module and exported members. The generator produces the concrete implementation automatically, ensuring type-safe, maintainable, and discoverable JS interop.

This package makes your JavaScript or TypeScript code the **source of truth** while eliminating boilerplate C# glue code.

---

## Features

- Strongly-typed JavaScript interop without manual wrappers
- Uses **source generators** for zero runtime reflection cost
- Auto-generated DI registration methods
- Supports:
  - Named exports (`export const Utilities = {...}`)
  - Default exports (`export default {...}`)
  - Async functions (`ValueTask<T>`)
  - Complex return types (deserialized via System.Text.Json)
- Avoids overloads and unsafe dynamic calls
- Works in Blazor Server and Blazor WebAssembly

---

## Installation

If you're consuming the package via NuGet:

`dotnet add package BlazorBridge`

This provides:

- A runtime assembly with attributes + marker interfaces
- A Roslyn analyzer containing the source generator

No additional configuration is required.

## Defining JS Interops
Example TypeScript/JavaScript module:

```typescript
// utilities.ts
export const Utilities = {
    focus(id: string, selector: string) {
        const root = document.querySelector(selector);
        const el = root?.querySelector(`#${id}`);
        el?.focus();
    },

    getRect(id: string) {
        const el = document.getElementById(id);
        return el?.getBoundingClientRect() ?? null;
    }
};
```

Then define the corresponding C# interface:
```csharp
using BlazorBridge;

[JsModule("./js/utilities.js")]
[JsExport("Utilities")]
public interface IUtilitiesInterop : IJsInterop
{
    [JsMethod("focus")]
    ValueTask FocusAsync(string id, string selector);

    [JsMethod("getRect")]
    ValueTask<DomRect?> GetRectAsync(string id);
}
```

The source generator will discover this interface and output:

- A concrete implementation (`UtilitiesInterop`)
- A DI extension method
- Internal invocation logic that loads the JS module only once

## Registering Generated Services
An extension method is also generated to allow one-line DI registration of the generated interops:
```csharp
builder.Services.AddJsInterops();
```

You can then inject them and call them like normal typed services:
```razor
@inject IUtilitiesInterop Utilities

<button @onclick="SetFocus">Focus</button>

@code {
    private async Task SetFocus()
    {
        await Utilities.FocusAsync("searchBox", "#root");
    }
}
```

## Using Default Exports
For JavaScript modules that use:
```ts
export default { ... }
```

Define the interface like this:
```csharp
using BlazorBridge;

[JsModule("./js/utilities-default.js")]
[JsDefaultExport]
public interface IDefaultUtilitiesJs : IJsInteropContract
{
    [JsMethod("focus")]
    ValueTask FocusAsync(string id, string selector);
}
```

The generator automatically targets `default.<methodName>`.

## Example DTO for Complex Types
Complex objects returned from JS are deserialized using `System.Text.Json`:
```csharp
public sealed class DomRect
{
    public double X { get; set; }
    public double Y { get; set; }
    public double Width { get; set; }
    public double Height { get; set; }
    public double Top { get; set; }
    public double Left { get; set; }
    public double Right { get; set; }
    public double Bottom { get; set; }
}
```
Return types are defined in the interface:
```csharp
using BlazorBridge;

[JsModule("./js/utilities.js")]
[JsExport("Utilities")]
public interface IUtilitiesInterop : IJsInterop
{
    [JsMethod("getRect")]
    ValueTask<DomRect?> GetRectAsync(string id);
}
```

## License
MIT