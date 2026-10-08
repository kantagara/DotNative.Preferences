using System;
using Microsoft.Extensions.DependencyInjection;

namespace DotNative.Preferences;

public static class PreferencesServiceProviderExtensions
{
#if NET10_0_OR_GREATER
    extension(IServiceProvider services)
    {
        /// <summary>Resolves the registered plugin using the provider's DI lifetime.</summary>
        public IPreferences Preferences => services.GetRequiredService<IPreferences>();
    }
#else
    /// <summary>Resolves the registered plugin using the provider's DI lifetime.</summary>
    public static IPreferences Preferences(this IServiceProvider services) =>
        services.GetRequiredService<IPreferences>();
#endif
}
