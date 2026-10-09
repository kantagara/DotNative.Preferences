using System.Text.Json;
using DotNative.Plugins;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace DotNative.Preferences;

public interface IPreferences
{
    Task<T> GetAsync<T>(string key, T fallback, CancellationToken cancellationToken = default);
    Task SetAsync<T>(string key, T value, CancellationToken cancellationToken = default);
    Task<bool> ContainsKeyAsync(string key, CancellationToken cancellationToken = default);
    Task RemoveAsync(string key, CancellationToken cancellationToken = default);
    Task ClearAsync(CancellationToken cancellationToken = default);
}

/// <summary>Atomic, process-coordinated preference storage. Never use for secrets.</summary>
public sealed class FilePreferences : IPreferences
{
    private readonly string path;

    public FilePreferences(string applicationId, PresentationTarget? target = null)
    {
        PlatformGuard.Desktop(target ?? PresentationTarget.Local);
        applicationId = PlatformGuard.Namespace(applicationId);
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var directory =
            OperatingSystem.IsMacOS()
                ? Path.Combine(home, "Library", "Application Support", applicationId)
            : OperatingSystem.IsLinux()
                ? Path.Combine(
                    Environment.GetEnvironmentVariable("XDG_DATA_HOME") is { Length: > 0 } xdg
                    && Path.IsPathFullyQualified(xdg)
                        ? xdg
                        : Path.Combine(home, ".local", "share"),
                    applicationId
                )
            : Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                applicationId
            );
        if (string.IsNullOrEmpty(home) || !Path.IsPathFullyQualified(directory))
            throw new DirectoryNotFoundException("Application data directory is unavailable.");
        Directory.CreateDirectory(directory);
        path = Path.Combine(directory, "preferences.json");
    }

    public Task<T> GetAsync<T>(
        string key,
        T fallback,
        CancellationToken cancellationToken = default
    )
    {
        Key(key);
        ValidateType<T>();
        return Access(
            false,
            map => map.TryGetValue(key, out var e) ? Decode<T>(e) : fallback,
            cancellationToken
        );
    }

    public Task SetAsync<T>(string key, T value, CancellationToken cancellationToken = default)
    {
        Key(key);
        ValidateType<T>();
        ArgumentNullException.ThrowIfNull(value);
        using var bytes = new MemoryStream();
        using (var writer = new Utf8JsonWriter(bytes))
        {
            switch (value)
            {
                case string s:
                    writer.WriteStringValue(s);
                    break;
                case bool b:
                    writer.WriteBooleanValue(b);
                    break;
                case int n:
                    writer.WriteNumberValue(n);
                    break;
                case long n:
                    writer.WriteNumberValue(n);
                    break;
                case double n when double.IsFinite(n):
                    writer.WriteNumberValue(n);
                    break;
                case string[] items:
                    writer.WriteStartArray();
                    foreach (var item in items)
                    {
                        ArgumentNullException.ThrowIfNull(item);
                        writer.WriteStringValue(item);
                    }
                    writer.WriteEndArray();
                    break;
                default:
                    throw new ArgumentException(
                        "Unsupported preference value or non-finite number."
                    );
            }
        }
        using var document = JsonDocument.Parse(bytes.ToArray());
        var element = document.RootElement.Clone();
        return Access(
            true,
            map =>
            {
                map[key] = element;
                return true;
            },
            cancellationToken
        );
    }

    public Task<bool> ContainsKeyAsync(string key, CancellationToken cancellationToken = default)
    {
        Key(key);
        return Access(false, map => map.ContainsKey(key), cancellationToken);
    }

    public async Task RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        Key(key);
        _ = await Access(true, map => map.Remove(key), cancellationToken).ConfigureAwait(false);
    }

    public async Task ClearAsync(CancellationToken cancellationToken = default)
    {
        _ = await Access(
                true,
                map =>
                {
                    map.Clear();
                    return true;
                },
                cancellationToken
            )
            .ConfigureAwait(false);
    }

    private async Task<T> Access<T>(
        bool write,
        Func<Dictionary<string, JsonElement>, T> action,
        CancellationToken token
    )
    {
        // The OS releases this exclusive handle on crash; no stale lock-file ownership.
        FileStream held;
        while (true)
        {
            token.ThrowIfCancellationRequested();
            try
            {
                held = new FileStream(
                    path + ".lock",
                    FileMode.OpenOrCreate,
                    FileAccess.ReadWrite,
                    FileShare.None
                );
                break;
            }
            catch (IOException error) when ((error.HResult & 0xffff) is 11 or 32 or 33 or 35)
            {
                await Task.Delay(25, token).ConfigureAwait(false);
            }
        }
        using (held)
        {
            var map = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
            if (File.Exists(path))
            {
                if (new FileInfo(path).Length > 4 * 1024 * 1024)
                    throw new InvalidDataException("Preference file exceeds 4 MiB.");
                using var document = JsonDocument.Parse(
                    await File.ReadAllBytesAsync(path, token).ConfigureAwait(false)
                );
                foreach (var entry in document.RootElement.EnumerateObject())
                    if (!map.TryAdd(entry.Name, entry.Value.Clone()))
                        throw new InvalidDataException("Duplicate preference key.");
            }
            var result = action(map);
            if (!write)
                return result;
            token.ThrowIfCancellationRequested();
            var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                using (
                    var stream = new FileStream(
                        temporary,
                        FileMode.CreateNew,
                        FileAccess.Write,
                        FileShare.None
                    )
                )
                {
                    using (var writer = new Utf8JsonWriter(stream))
                    {
                        writer.WriteStartObject();
                        foreach (var (key, value) in map)
                        {
                            writer.WritePropertyName(key);
                            value.WriteTo(writer);
                        }
                        writer.WriteEndObject();
                    }
                    if (stream.Length > 4 * 1024 * 1024)
                        throw new InvalidDataException("Preferences exceed 4 MiB.");
                    stream.Flush(true);
                }
                File.Move(temporary, path, true);
            }
            finally
            {
                if (File.Exists(temporary))
                    File.Delete(temporary);
            }
            return result;
        }
    }

    private static void Key(string key)
    {
        ArgumentException.ThrowIfNullOrEmpty(key);
        if (key.Length > 1024)
            throw new ArgumentException("Key exceeds 1024 characters.");
    }

    private static void ValidateType<T>()
    {
        if (
            typeof(T) != typeof(string)
            && typeof(T) != typeof(bool)
            && typeof(T) != typeof(int)
            && typeof(T) != typeof(long)
            && typeof(T) != typeof(double)
            && typeof(T) != typeof(string[])
        )
            throw new NotSupportedException(
                "Preferences support string, bool, int, long, finite double and string[]."
            );
    }

    private static T Decode<T>(JsonElement value)
    {
        object? result =
            typeof(T) == typeof(string) ? value.GetString()
            : typeof(T) == typeof(bool) ? value.GetBoolean()
            : typeof(T) == typeof(int) ? value.GetInt32()
            : typeof(T) == typeof(long) ? value.GetInt64()
            : typeof(T) == typeof(double) ? value.GetDouble()
            : value
                .EnumerateArray()
                .Select(x =>
                    x.GetString() ?? throw new InvalidDataException("Null preference array item.")
                )
                .ToArray();
        return (T)(result ?? throw new InvalidDataException("Null preference value."));
    }
}

public static class PreferencesServices
{
    public static IServiceCollection AddPreferences(
        this IServiceCollection services,
        string applicationId
    )
    {
        services.TryAddSingleton<IPreferences>(p =>
            (p.GetService<PresentationTarget>() ?? PresentationTarget.Local).Platform
                is NativePlatform.IOS
                    or NativePlatform.Android
                ? new ChannelPreferences(p.GetRequiredService<IPlatformChannels>(), applicationId)
                : new FilePreferences(applicationId, p.GetService<PresentationTarget>())
        );
        return services;
    }
}
