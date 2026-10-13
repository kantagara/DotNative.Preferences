# DotNative.Preferences

Typed app-scoped preferences for Android, iOS, macOS, Windows and Linux. Mobile uses native channels backed by SharedPreferences on Android and UserDefaults on iOS; desktop keeps atomic file storage. Keys are isolated by application ID. These stores are for settings, not secrets.

```csharp
var preferences = Preferences.Create(channels, target);
await preferences.SetAsync("theme", "dark", cancellationToken);
var theme = await preferences.GetAsync("theme", "system", cancellationToken);
```

Supported values are `string`, `bool`, `int`, `long`, finite `double` and `string[]`. Missing keys return the supplied fallback. Wrong-type or malformed stored values throw instead of being coerced. `RemoveAsync`, `ContainsKeyAsync` and `ClearAsync` cover deletion and enumeration needs. Calls accept cancellation; a cancellation before the atomic file replacement prevents the write, while a committed write is reported as complete.

Desktop storage is a versionable UTF-8 JSON file under the app's application-data directory. A cross-process exclusive lock and write-to-temp/rename prevent lost concurrent updates and partial files. The file has a 4 MiB limit. Preferences are ordinary data and **are not encrypted**; put credentials in SecureStorage. Rapid calls serialize for consistent read/modify/write behavior.

Build locally: `dotnet build -p:DotNativeSourceRoot=../dotNative`. macOS, Windows 11 ARM64, and Linux ARM64 (Debian 12 container) passed typed roundtrip and concurrent access checks.

## Direct use

`Preferences.Create(channels, target, applicationId: null)` creates the correct
implementation without DI registration. Keep the instance in the class that uses
it. Pass the active host's `IPlatformChannels` and `PresentationTarget`; no global
host is retained. Native storage defaults to the application namespace. Desktop
storage uses the entry assembly name unless an explicit application ID is supplied.
Desktop callers may pass null channels. Existing `AddPreferences` registration remains optional.

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

## v0.1.0-preview.3 validation

Android handlers reject missing/non-map arguments before accessing fields. The NuGet
asset now includes Android/iOS sources and buildTransitive registration rules.

```sh
dotnet run --project tests/Managed -f net10.0
DOTNET_ROLL_FORWARD=Major dotnet run --project tests/Managed -f net9.0
ANDROID_HOME=/path/to/android/sdk bash tests/native/android/gradlew -p tests/native/android connectedDebugAndroidTest
```

The Android test uses real SharedPreferences with a channel/codec fixture; it checks
malformed arguments, reopen persistence, namespace isolation and removal. It is not
a complete host transport or application E2E test.
