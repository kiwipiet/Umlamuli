namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
///     Compatibility Extensions to simplify the move from MediatR to Umlamuli.
/// </summary>
public static class ServiceCollectionCompatibilityExtensions
{
    /// <summary>
    ///     Registers handlers and mediator types from the specified assemblies
    /// </summary>
    /// <param name="services">Service collection</param>
    /// <param name="configuration">The action used to configure the options</param>
    /// <returns>Service collection</returns>
    [Obsolete("Use AddUmlamuli instead.")]
    public static IServiceCollection AddMediatR(this IServiceCollection services,
        Action<UmlamuliServiceConfiguration> configuration) =>
        services.AddUmlamuli(configuration);

    /// <summary>
    ///     Registers handlers and mediator types from the specified assemblies
    /// </summary>
    /// <param name="services">Service collection</param>
    /// <param name="configuration">Configuration options</param>
    /// <returns>Service collection</returns>
    [Obsolete("Use AddUmlamuli instead.")]
    public static IServiceCollection AddMediatR(this IServiceCollection services,
        UmlamuliServiceConfiguration configuration) =>
        services.AddUmlamuli(configuration);
}