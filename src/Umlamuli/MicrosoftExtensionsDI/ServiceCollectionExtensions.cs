using Umlamuli;
using Umlamuli.Pipeline;
using Umlamuli.Registration;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
///     Extensions to scan for Umlamuli handlers and registers them.
///     - Scans for any handler interface implementations and registers them as <see cref="ServiceLifetime.Transient" />
///     - Scans for any <see cref="IRequestPreProcessor{TRequest}" /> and
///     <see cref="IRequestPostProcessor{TRequest,TResponse}" /> implementations and registers them as transient instances
///     Registers <see cref="IMediator" /> as a transient instance
///     After calling AddUmlamuli you can use the container to resolve an <see cref="IMediator" /> instance.
///     This does not scan for any <see cref="IPipelineBehavior{TRequest,TResponse}" /> instances including
///     <see cref="RequestPreProcessorBehavior{TRequest,TResponse}" /> and
///     <see cref="RequestPreProcessorBehavior{TRequest,TResponse}" />.
///     To register behaviors, use the
///     <see cref="ServiceCollectionServiceExtensions.AddTransient(IServiceCollection,Type,Type)" /> with the open generic
///     or closed generic types.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    ///     Registers handlers and mediator types from the specified assemblies
    /// </summary>
    /// <param name="services">Service collection</param>
    /// <param name="configuration">The action used to configure the options</param>
    /// <returns>Service collection</returns>
    public static IServiceCollection AddUmlamuli(this IServiceCollection services,
        Action<UmlamuliServiceConfiguration> configuration)
    {
        if (configuration == null) throw new ArgumentNullException(nameof(configuration));

        var serviceConfig = new UmlamuliServiceConfiguration();

        configuration.Invoke(serviceConfig);

        return services.AddUmlamuli(serviceConfig);
    }

    /// <summary>
    ///     Registers handlers and mediator types from the specified assemblies
    /// </summary>
    /// <param name="services">Service collection</param>
    /// <param name="configuration">Configuration options</param>
    /// <returns>Service collection</returns>
    public static IServiceCollection AddUmlamuli(this IServiceCollection services,
        UmlamuliServiceConfiguration configuration)
    {
        if (configuration == null) throw new ArgumentNullException(nameof(configuration));

        if (!configuration.AssembliesToRegister.Any())
            throw new ArgumentException(
                "No assemblies found to scan. Supply at least one assembly to scan for handlers.");

        ServiceRegistrar.SetGenericRequestHandlerRegistrationLimitations(configuration);

        ServiceRegistrar.AddUmlamuliClassesWithTimeout(services, configuration);

        ServiceRegistrar.AddRequiredServices(services, configuration);

        return services;
    }
}