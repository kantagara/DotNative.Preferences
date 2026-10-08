# DotNative.Preferences

Typed app-scoped preferences for macOS, Windows and Linux, with one managed API across the five planned DotNative platforms. Android/iOS currently throw `PlatformNotSupportedException` because native app preference stores are not connected yet.

```csharp
builder.Services.AddPreferences("com.example.myapp");
var preferences = provider.Preferences;
await preferences.SetAsync("theme", "dark", cancellationToken);
var theme = await preferences.GetAsync("theme", "system", cancellationToken);
```

Supported values are `string`, `bool`, `int`, `long`, finite `double` and `string[]`. Missing keys return the supplied fallback. Wrong-type or malformed stored values throw instead of being coerced. `RemoveAsync`, `ContainsKeyAsync` and `ClearAsync` cover deletion and enumeration needs. Calls accept cancellation; a cancellation before the atomic file replacement prevents the write, while a committed write is reported as complete.

Desktop storage is a versionable UTF-8 JSON file under the app's application-data directory. A cross-process exclusive lock and write-to-temp/rename prevent lost concurrent updates and partial files. The file has a 4 MiB limit. Preferences are ordinary data and **are not encrypted**; put credentials in SecureStorage. Rapid calls serialize for consistent read/modify/write behavior.

Build locally: `dotnet build -p:DotNativeSourceRoot=../dotNative`. macOS, Windows 11 ARM64, and Linux ARM64 (Debian 12 container) passed typed roundtrip and concurrent access checks.

## Service access

Import `DotNative.Preferences` to access the plugin through `IServiceProvider`:

```csharp
using DotNative.Preferences;

var plugin = services.Preferences;
```

The getter calls `GetRequiredService<IPreferences>()` on every access, preserving
DI lifetimes and the usual missing-registration error. Register the plugin with
`AddPreferences(...)` before building the provider.

A `net10.0` application uses the property syntax with C# 14 or later. A
`net9.0` application uses only the method equivalent:

```csharp
var plugin = services.Preferences();
```

The package contains separate `net9.0` and `net10.0` assemblies. NuGet selects
the assembly matching the application target framework. `NET10_0_OR_GREATER`
selects the property; the `#else` branch selects the method.

Build and pack both targets with .NET 10 SDK. A source build using .NET 9 SDK
builds only `net9.0`; it does not produce the .NET 10 assembly.
