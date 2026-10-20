using System;

namespace Contr.UI.Contracts.Services;

/// <summary>
/// The service provider for shared UI types the XAML runtime constructs with <c>new()</c>
/// (behaviors, converters), which cannot receive constructor injection.
/// </summary>
/// <remarks>
/// Everything else uses constructor injection. The host configures this once at startup.
/// </remarks>
public static class ContractServices
{
    private static IServiceProvider? _provider;

    /// <summary>The configured service provider.</summary>
    /// <exception cref="InvalidOperationException">Thrown if accessed before <see cref="Configure"/>.</exception>
    public static IServiceProvider Provider =>
        _provider
        ?? throw new InvalidOperationException(
            "ContractServices has not been configured. Call ContractServices.Configure(...) during startup.");

    /// <summary>Binds the accessor to the application's service provider. Call once, at startup.</summary>
    public static void Configure(IServiceProvider provider)
    {
        ArgumentNullException.ThrowIfNull(provider);
        _provider = provider;
    }
}
