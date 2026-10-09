using DotNative.Plugins;

namespace DotNative.Preferences;

internal sealed class ChannelPreferences(IPlatformChannels channels, string applicationId)
    : IPreferences
{
    private readonly string applicationId = PlatformGuard.Namespace(applicationId);
    private readonly MethodChannel channel = channels.Get("dotnative.preferences");

    private Task<object?> Invoke(string method, string? key, object? value, CancellationToken token)
    {
        if (
            key is not null
            && (string.IsNullOrWhiteSpace(key) || key.Length > 1024 || key.Contains('\0'))
        )
            throw new ArgumentException("Invalid preference key.", nameof(key));
        return channel.InvokeAsync(
            method,
            new Dictionary<string, object?>
            {
                ["applicationId"] = applicationId,
                ["key"] = key,
                ["value"] = value,
            },
            token
        );
    }

    public async Task<T> GetAsync<T>(
        string key,
        T fallback,
        CancellationToken cancellationToken = default
    )
    {
        if (
            typeof(T) != typeof(string)
            && typeof(T) != typeof(bool)
            && typeof(T) != typeof(int)
            && typeof(T) != typeof(long)
            && typeof(T) != typeof(double)
            && typeof(T) != typeof(string[])
        )
            throw new ArgumentException("Unsupported preference type.");
        var value = await Invoke("get", key, null, cancellationToken).ConfigureAwait(false);
        if (value is null)
            return fallback;
        object result = value switch
        {
            long n when typeof(T) == typeof(int) => checked((int)n),
            IReadOnlyList<object?> items when typeof(T) == typeof(string[]) => items
                .Select(item =>
                    item as string ?? throw new InvalidDataException("Invalid preference array.")
                )
                .ToArray(),
            _ => value,
        };
        return result is T typed
            ? typed
            : throw new InvalidDataException("Preference type mismatch.");
    }

    public async Task SetAsync<T>(
        string key,
        T value,
        CancellationToken cancellationToken = default
    )
    {
        if (value is not (string or bool or int or long or double or string[]))
            throw new ArgumentException("Unsupported preference type.");
        if (value is double number && !double.IsFinite(number))
            throw new ArgumentOutOfRangeException(nameof(value));
        if (value is string[] items && items.Any(item => item is null))
            throw new ArgumentException("Preference array cannot contain null.");
        await Invoke("set", key, value, cancellationToken).ConfigureAwait(false);
    }

    public async Task<bool> ContainsKeyAsync(
        string key,
        CancellationToken cancellationToken = default
    ) => await Invoke("contains", key, null, cancellationToken).ConfigureAwait(false) is true;

    public async Task RemoveAsync(string key, CancellationToken cancellationToken = default) =>
        await Invoke("remove", key, null, cancellationToken).ConfigureAwait(false);

    public async Task ClearAsync(CancellationToken cancellationToken = default) =>
        await Invoke("clear", null, null, cancellationToken).ConfigureAwait(false);
}
