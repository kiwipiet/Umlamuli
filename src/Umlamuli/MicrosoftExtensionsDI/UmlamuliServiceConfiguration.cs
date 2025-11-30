using System.Reflection;
using Umlamuli;
using Umlamuli.Entities;
using Umlamuli.NotificationPublishers;
using Umlamuli.Pipeline;
using Umlamuli.Registration;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
///     Provides a fluent configuration surface for registering Umlamuli mediator components
///     (handlers, behaviors, processors, publishers) into the Microsoft.Extensions.DependencyInjection
///     container.
/// </summary>
/// <remarks>
///     Typical usage chains multiple registration calls before the final registration extension
///     applies these descriptors to an <see cref="IServiceCollection" />.
///     Each Add* method returns the same <see cref="UmlamuliServiceConfiguration" /> instance enabling
///     fluent chaining.
/// </remarks>
public class UmlamuliServiceConfiguration
{
    /// <summary>
    ///     Optional filter to evaluate whether a discovered type should be registered.
    ///     Return <c>true</c> to include the type; <c>false</c> to exclude.
    ///     Default value always returns <c>true</c>.
    /// </summary>
    public Func<Type, bool> TypeEvaluator { get; set; } = _ => true;

    /// <summary>
    ///     The concrete mediator implementation type to register (must implement the mediator contract).
    ///     Defaults to <see cref="Mediator" />.
    /// </summary>
    public Type MediatorImplementationType { get; set; } = typeof(Mediator);

    /// <summary>
    ///     Strategy instance used to publish notifications. If <see cref="NotificationPublisherType" />
    ///     is specified, that type overrides this instance registration.
    ///     Defaults to <see cref="ForeachAwaitPublisher" />.
    /// </summary>
    public INotificationPublisher NotificationPublisher { get; set; } = new ForeachAwaitPublisher();

    /// <summary>
    ///     Optional type implementing <see cref="INotificationPublisher" /> to register instead of
    ///     the concrete instance supplied via <see cref="NotificationPublisher" />.
    ///     When set this overrides <see cref="NotificationPublisher" />.
    /// </summary>
    public Type? NotificationPublisherType { get; set; }

    /// <summary>
    ///     The <see cref="ServiceLifetime" /> applied to mediator related service registrations unless
    ///     overridden per specific descriptor. Defaults to <see cref="ServiceLifetime.Transient" />.
    /// </summary>
    public ServiceLifetime Lifetime { get; set; } = ServiceLifetime.Transient;

    /// <summary>
    ///     Strategy controlling when request exception actions are executed in the pipeline.
    ///     Default is <see cref="DependencyInjection.RequestExceptionActionProcessorStrategy.ApplyForUnhandledExceptions" />.
    /// </summary>
    public RequestExceptionActionProcessorStrategy RequestExceptionActionProcessorStrategy { get; set; }
        = RequestExceptionActionProcessorStrategy.ApplyForUnhandledExceptions;

    /// <summary>
    ///     Assemblies that will be scanned for handlers, behaviors, processors and related mediator components.
    /// </summary>
    internal List<Assembly> AssembliesToRegister { get; } = new();

    /// <summary>
    ///     Ordered list of pipeline behavior service descriptors to be registered.
    ///     Order matters for pipeline execution.
    /// </summary>
    public List<ServiceDescriptor> BehaviorsToRegister { get; } = new();

    /// <summary>
    ///     Ordered list of stream pipeline behavior service descriptors to be registered.
    /// </summary>
    public List<ServiceDescriptor> StreamBehaviorsToRegister { get; } = new();

    /// <summary>
    ///     Ordered list of request pre-processor service descriptors to be registered.
    ///     Executed before main request handler.
    /// </summary>
    public List<ServiceDescriptor> RequestPreProcessorsToRegister { get; } = new();

    /// <summary>
    ///     Ordered list of request post-processor service descriptors to be registered.
    ///     Executed after main request handler.
    /// </summary>
    public List<ServiceDescriptor> RequestPostProcessorsToRegister { get; } = new();

    /// <summary>
    ///     Indicates whether request pre-/post-processors should be auto-registered during assembly scanning.
    /// </summary>
    public bool AutoRegisterRequestProcessors { get; set; }

    /// <summary>
    ///     Maximum number of generic type parameters that a generic request handler may define.
    ///     Set to 0 to disable the constraint.
    /// </summary>
    public int MaxGenericTypeParameters { get; set; } = 10;

    /// <summary>
    ///     Maximum number of concrete types allowed to close a generic request type parameter constraint.
    ///     Set to 0 to disable the constraint.
    /// </summary>
    public int MaxTypesClosing { get; set; } = 100;

    /// <summary>
    ///     Maximum number of generic request handler registrations attempted.
    ///     Set to 0 to disable the constraint.
    /// </summary>
    public int MaxGenericTypeRegistrations { get; set; } = 125000;

    /// <summary>
    ///     Timeout in milliseconds for the generic handler registration process before failing.
    ///     Set to 0 to disable the constraint.
    /// </summary>
    public int RegistrationTimeout { get; set; } = 15000;

    /// <summary>
    ///     Flag indicating whether generic handlers containing open generic type parameters should be registered.
    /// </summary>
    public bool RegisterGenericHandlers { get; set; } = false;

    /// <summary>
    ///     Registers mediator related services from the assembly containing the specified generic type.
    /// </summary>
    /// <typeparam name="T">A type whose containing assembly will be scanned.</typeparam>
    /// <returns>The same configuration instance for fluent chaining.</returns>
    public UmlamuliServiceConfiguration RegisterServicesFromAssemblyContaining<T>()
        => RegisterServicesFromAssemblyContaining(typeof(T));

    /// <summary>
    ///     Registers mediator related services from the assembly containing the specified type.
    /// </summary>
    /// <param name="type">Type whose containing assembly will be scanned.</param>
    /// <returns>The same configuration instance for fluent chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="type" /> is null.</exception>
    public UmlamuliServiceConfiguration RegisterServicesFromAssemblyContaining(Type type)
    {
        if (type == null) throw new ArgumentNullException(nameof(type));
        return RegisterServicesFromAssembly(type.Assembly);
    }

    /// <summary>
    ///     Registers mediator related services by scanning a single assembly.
    /// </summary>
    /// <param name="assembly">Assembly to scan.</param>
    /// <returns>The same configuration instance for fluent chaining.</returns>
    public UmlamuliServiceConfiguration RegisterServicesFromAssembly(Assembly assembly)
    {
        AssembliesToRegister.Add(assembly);
        return this;
    }

    /// <summary>
    ///     Registers mediator related services by scanning multiple assemblies.
    /// </summary>
    /// <param name="assemblies">Assemblies to scan.</param>
    /// <returns>The same configuration instance for fluent chaining.</returns>
    public UmlamuliServiceConfiguration RegisterServicesFromAssemblies(
        params Assembly[] assemblies)
    {
        AssembliesToRegister.AddRange(assemblies);
        return this;
    }

    /// <summary>
    ///     Registers a closed generic pipeline behavior implementation against a specific closed behavior interface.
    /// </summary>
    /// <typeparam name="TServiceType">Closed pipeline behavior interface.</typeparam>
    /// <typeparam name="TImplementationType">Closed pipeline behavior implementation.</typeparam>
    /// <param name="serviceLifetime">Optional lifetime override.</param>
    /// <returns>The same configuration instance for fluent chaining.</returns>
    public UmlamuliServiceConfiguration AddBehavior<TServiceType, TImplementationType>(
        ServiceLifetime serviceLifetime = ServiceLifetime.Transient)
        => AddBehavior(typeof(TServiceType), typeof(TImplementationType), serviceLifetime);

    /// <summary>
    ///     Registers a closed pipeline behavior implementation against each closed
    ///     <see cref="IPipelineBehavior{TRequest,TResponse}" /> interface it implements.
    /// </summary>
    /// <typeparam name="TImplementationType">Closed pipeline behavior implementation.</typeparam>
    /// <param name="serviceLifetime">Optional lifetime override.</param>
    /// <returns>The same configuration instance for fluent chaining.</returns>
    public UmlamuliServiceConfiguration AddBehavior<TImplementationType>(
        ServiceLifetime serviceLifetime = ServiceLifetime.Transient) =>
        AddBehavior(typeof(TImplementationType), serviceLifetime);

    /// <summary>
    ///     Registers a closed pipeline behavior implementation against each closed
    ///     <see cref="IPipelineBehavior{TRequest,TResponse}" /> interface it implements.
    /// </summary>
    /// <param name="implementationType">Closed pipeline behavior implementation type.</param>
    /// <param name="serviceLifetime">Optional lifetime override.</param>
    /// <returns>The same configuration instance for fluent chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="implementationType" /> is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the type does not implement any pipeline behavior interfaces.</exception>
    public UmlamuliServiceConfiguration AddBehavior(Type implementationType,
        ServiceLifetime serviceLifetime = ServiceLifetime.Transient)
    {
        if (implementationType == null) throw new ArgumentNullException(nameof(implementationType));

        var implementedGenericInterfaces =
            implementationType.FindInterfacesThatClose(typeof(IPipelineBehavior<,>)).ToList();

        if (implementedGenericInterfaces.Count == 0)
            throw new InvalidOperationException(
                $"{implementationType.Name} must implement {typeof(IPipelineBehavior<,>).FullName}");

        foreach (var implementedBehaviorType in implementedGenericInterfaces)
            BehaviorsToRegister.Add(new ServiceDescriptor(implementedBehaviorType, implementationType,
                serviceLifetime));

        return this;
    }

    /// <summary>
    ///     Registers a closed pipeline behavior mapping between an interface and implementation.
    /// </summary>
    /// <param name="serviceType">Closed pipeline behavior service (interface) type.</param>
    /// <param name="implementationType">Closed pipeline behavior implementation type.</param>
    /// <param name="serviceLifetime">Optional lifetime override.</param>
    /// <returns>The same configuration instance for fluent chaining.</returns>
    public UmlamuliServiceConfiguration AddBehavior(Type serviceType, Type implementationType,
        ServiceLifetime serviceLifetime = ServiceLifetime.Transient)
    {
        BehaviorsToRegister.Add(new ServiceDescriptor(serviceType, implementationType, serviceLifetime));
        return this;
    }

    /// <summary>
    ///     Registers an open generic pipeline behavior type against the open
    ///     <see cref="IPipelineBehavior{TRequest,TResponse}" /> interface.
    /// </summary>
    /// <param name="openBehaviorType">Open generic behavior type.</param>
    /// <param name="serviceLifetime">Optional lifetime override.</param>
    /// <returns>The same configuration instance for fluent chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="openBehaviorType" /> is null.</exception>
    /// <exception cref="InvalidOperationException">
    ///     Thrown when the type is not generic or does not implement the required
    ///     interface.
    /// </exception>
    public UmlamuliServiceConfiguration AddOpenBehavior(Type openBehaviorType,
        ServiceLifetime serviceLifetime = ServiceLifetime.Transient)
    {
        if (openBehaviorType == null) throw new ArgumentNullException(nameof(openBehaviorType));
        if (!openBehaviorType.IsGenericType)
            throw new InvalidOperationException($"{openBehaviorType.Name} must be generic");

        var implementedGenericInterfaces = openBehaviorType.GetInterfaces().Where(i => i.IsGenericType)
            .Select(i => i.GetGenericTypeDefinition());
        var implementedOpenBehaviorInterfaces =
            new HashSet<Type>(implementedGenericInterfaces.Where(i => i == typeof(IPipelineBehavior<,>)));

        if (implementedOpenBehaviorInterfaces.Count == 0)
            throw new InvalidOperationException(
                $"{openBehaviorType.Name} must implement {typeof(IPipelineBehavior<,>).FullName}");

        foreach (var openBehaviorInterface in implementedOpenBehaviorInterfaces)
            BehaviorsToRegister.Add(new ServiceDescriptor(openBehaviorInterface, openBehaviorType, serviceLifetime));

        return this;
    }

    /// <summary>
    ///     Registers multiple open generic pipeline behavior types against the open
    ///     <see cref="IPipelineBehavior{TRequest,TResponse}" /> interface.
    /// </summary>
    /// <param name="openBehaviorTypes">Enumerable of open generic behavior types.</param>
    /// <param name="serviceLifetime">Optional lifetime override applied to all.</param>
    /// <returns>The same configuration instance for fluent chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="openBehaviorTypes" /> is null.</exception>
    public UmlamuliServiceConfiguration AddOpenBehaviors(IEnumerable<Type> openBehaviorTypes,
        ServiceLifetime serviceLifetime = ServiceLifetime.Transient)
    {
        if (openBehaviorTypes == null) throw new ArgumentNullException(nameof(openBehaviorTypes));
        foreach (var openBehaviorType in openBehaviorTypes) AddOpenBehavior(openBehaviorType, serviceLifetime);

        return this;
    }

    /// <summary>
    ///     Registers multiple open generic pipeline behaviors provided via <see cref="OpenBehavior" /> descriptors.
    /// </summary>
    /// <param name="openBehaviors">Collection of <see cref="OpenBehavior" /> instances.</param>
    /// <returns>The same configuration instance for fluent chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="openBehaviors" /> is null.</exception>
    public UmlamuliServiceConfiguration AddOpenBehaviors(IEnumerable<OpenBehavior> openBehaviors)
    {
        if (openBehaviors == null) throw new ArgumentNullException(nameof(openBehaviors));
        foreach (var openBehavior in openBehaviors)
            AddOpenBehavior(openBehavior.OpenBehaviorType!, openBehavior.ServiceLifetime);

        return this;
    }

    /// <summary>
    ///     Registers a closed stream pipeline behavior mapping between interface and implementation.
    /// </summary>
    /// <typeparam name="TServiceType">Closed stream behavior interface.</typeparam>
    /// <typeparam name="TImplementationType">Closed stream behavior implementation.</typeparam>
    /// <param name="serviceLifetime">Optional lifetime override.</param>
    /// <returns>The same configuration instance for fluent chaining.</returns>
    public UmlamuliServiceConfiguration AddStreamBehavior<TServiceType, TImplementationType>(
        ServiceLifetime serviceLifetime = ServiceLifetime.Transient)
        => AddStreamBehavior(typeof(TServiceType), typeof(TImplementationType), serviceLifetime);

    /// <summary>
    ///     Registers a closed stream pipeline behavior mapping between interface and implementation.
    /// </summary>
    /// <param name="serviceType">Closed stream behavior interface type.</param>
    /// <param name="implementationType">Closed stream behavior implementation type.</param>
    /// <param name="serviceLifetime">Optional lifetime override.</param>
    /// <returns>The same configuration instance for fluent chaining.</returns>
    public UmlamuliServiceConfiguration AddStreamBehavior(Type serviceType, Type implementationType,
        ServiceLifetime serviceLifetime = ServiceLifetime.Transient)
    {
        StreamBehaviorsToRegister.Add(new ServiceDescriptor(serviceType, implementationType, serviceLifetime));
        return this;
    }

    /// <summary>
    ///     Registers a closed stream pipeline behavior implementation against each closed
    ///     <see cref="IStreamPipelineBehavior{TRequest,TResponse}" /> it implements.
    /// </summary>
    /// <typeparam name="TImplementationType">Closed stream behavior implementation.</typeparam>
    /// <param name="serviceLifetime">Optional lifetime override.</param>
    /// <returns>The same configuration instance for fluent chaining.</returns>
    public UmlamuliServiceConfiguration AddStreamBehavior<TImplementationType>(
        ServiceLifetime serviceLifetime = ServiceLifetime.Transient)
        => AddStreamBehavior(typeof(TImplementationType), serviceLifetime);

    /// <summary>
    ///     Registers a closed stream pipeline behavior implementation against each closed
    ///     <see cref="IStreamPipelineBehavior{TRequest,TResponse}" /> interface it implements.
    /// </summary>
    /// <param name="implementationType">Closed stream behavior implementation type.</param>
    /// <param name="serviceLifetime">Optional lifetime override.</param>
    /// <returns>The same configuration instance for fluent chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="implementationType" /> is null.</exception>
    /// <exception cref="InvalidOperationException">
    ///     Thrown when the type does not implement any stream pipeline behavior
    ///     interfaces.
    /// </exception>
    public UmlamuliServiceConfiguration AddStreamBehavior(Type implementationType,
        ServiceLifetime serviceLifetime = ServiceLifetime.Transient)
    {
        if (implementationType == null) throw new ArgumentNullException(nameof(implementationType));
        var implementedGenericInterfaces =
            implementationType.FindInterfacesThatClose(typeof(IStreamPipelineBehavior<,>)).ToList();

        if (implementedGenericInterfaces.Count == 0)
            throw new InvalidOperationException(
                $"{implementationType.Name} must implement {typeof(IStreamPipelineBehavior<,>).FullName}");

        foreach (var implementedBehaviorType in implementedGenericInterfaces)
            StreamBehaviorsToRegister.Add(new ServiceDescriptor(implementedBehaviorType, implementationType,
                serviceLifetime));

        return this;
    }

    /// <summary>
    ///     Registers an open generic stream pipeline behavior type against the open
    ///     <see cref="IStreamPipelineBehavior{TRequest,TResponse}" /> interface.
    /// </summary>
    /// <param name="openBehaviorType">Open generic stream behavior type.</param>
    /// <param name="serviceLifetime">Optional lifetime override.</param>
    /// <returns>The same configuration instance for fluent chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="openBehaviorType" /> is null.</exception>
    /// <exception cref="InvalidOperationException">
    ///     Thrown when the type is not generic or does not implement the required
    ///     interface.
    /// </exception>
    public UmlamuliServiceConfiguration AddOpenStreamBehavior(Type openBehaviorType,
        ServiceLifetime serviceLifetime = ServiceLifetime.Transient)
    {
        if (openBehaviorType == null) throw new ArgumentNullException(nameof(openBehaviorType));
        if (!openBehaviorType.IsGenericType)
            throw new InvalidOperationException($"{openBehaviorType.Name} must be generic");

        var implementedGenericInterfaces = openBehaviorType.GetInterfaces().Where(i => i.IsGenericType)
            .Select(i => i.GetGenericTypeDefinition());
        var implementedOpenBehaviorInterfaces =
            new HashSet<Type>(implementedGenericInterfaces.Where(i => i == typeof(IStreamPipelineBehavior<,>)));

        if (implementedOpenBehaviorInterfaces.Count == 0)
            throw new InvalidOperationException(
                $"{openBehaviorType.Name} must implement {typeof(IStreamPipelineBehavior<,>).FullName}");

        foreach (var openBehaviorInterface in implementedOpenBehaviorInterfaces)
            StreamBehaviorsToRegister.Add(new ServiceDescriptor(openBehaviorInterface, openBehaviorType,
                serviceLifetime));

        return this;
    }

    /// <summary>
    ///     Registers a closed request pre-processor mapping between interface and implementation.
    /// </summary>
    /// <typeparam name="TServiceType">Closed request pre-processor interface.</typeparam>
    /// <typeparam name="TImplementationType">Closed request pre-processor implementation.</typeparam>
    /// <param name="serviceLifetime">Optional lifetime override.</param>
    /// <returns>The same configuration instance for fluent chaining.</returns>
    public UmlamuliServiceConfiguration AddRequestPreProcessor<TServiceType, TImplementationType>(
        ServiceLifetime serviceLifetime = ServiceLifetime.Transient)
        => AddRequestPreProcessor(typeof(TServiceType), typeof(TImplementationType), serviceLifetime);

    /// <summary>
    ///     Registers a closed request pre-processor mapping between interface and implementation.
    /// </summary>
    /// <param name="serviceType">Closed request pre-processor interface type.</param>
    /// <param name="implementationType">Closed request pre-processor implementation type.</param>
    /// <param name="serviceLifetime">Optional lifetime override.</param>
    /// <returns>The same configuration instance for fluent chaining.</returns>
    public UmlamuliServiceConfiguration AddRequestPreProcessor(Type serviceType, Type implementationType,
        ServiceLifetime serviceLifetime = ServiceLifetime.Transient)
    {
        RequestPreProcessorsToRegister.Add(new ServiceDescriptor(serviceType, implementationType, serviceLifetime));
        return this;
    }

    /// <summary>
    ///     Registers a closed request pre-processor implementation against each closed
    ///     <see cref="IRequestPreProcessor{TRequest}" /> interface it implements.
    /// </summary>
    /// <typeparam name="TImplementationType">Closed request pre-processor implementation type.</typeparam>
    /// <param name="serviceLifetime">Optional lifetime override.</param>
    /// <returns>The same configuration instance for fluent chaining.</returns>
    public UmlamuliServiceConfiguration AddRequestPreProcessor<TImplementationType>(
        ServiceLifetime serviceLifetime = ServiceLifetime.Transient)
        => AddRequestPreProcessor(typeof(TImplementationType), serviceLifetime);

    /// <summary>
    ///     Registers a closed request pre-processor implementation against each closed
    ///     <see cref="IRequestPreProcessor{TRequest}" /> interface it implements.
    /// </summary>
    /// <param name="implementationType">Closed request pre-processor implementation type.</param>
    /// <param name="serviceLifetime">Optional lifetime override.</param>
    /// <returns>The same configuration instance for fluent chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="implementationType" /> is null.</exception>
    /// <exception cref="InvalidOperationException">
    ///     Thrown when the type does not implement any request pre-processor
    ///     interfaces.
    /// </exception>
    public UmlamuliServiceConfiguration AddRequestPreProcessor(Type implementationType,
        ServiceLifetime serviceLifetime = ServiceLifetime.Transient)
    {
        if (implementationType == null) throw new ArgumentNullException(nameof(implementationType));
        var implementedGenericInterfaces =
            implementationType.FindInterfacesThatClose(typeof(IRequestPreProcessor<>)).ToList();

        if (implementedGenericInterfaces.Count == 0)
            throw new InvalidOperationException(
                $"{implementationType.Name} must implement {typeof(IRequestPreProcessor<>).FullName}");

        foreach (var implementedPreProcessorType in implementedGenericInterfaces)
            RequestPreProcessorsToRegister.Add(new ServiceDescriptor(implementedPreProcessorType, implementationType,
                serviceLifetime));

        return this;
    }

    /// <summary>
    ///     Registers an open generic request pre-processor type against the open <see cref="IRequestPreProcessor{TRequest}" />
    ///     interface.
    /// </summary>
    /// <param name="openBehaviorType">Open generic request pre-processor type.</param>
    /// <param name="serviceLifetime">Optional lifetime override.</param>
    /// <returns>The same configuration instance for fluent chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="openBehaviorType" /> is null.</exception>
    /// <exception cref="InvalidOperationException">
    ///     Thrown when the type is not generic or does not implement the required
    ///     interface.
    /// </exception>
    public UmlamuliServiceConfiguration AddOpenRequestPreProcessor(Type openBehaviorType,
        ServiceLifetime serviceLifetime = ServiceLifetime.Transient)
    {
        if (openBehaviorType == null) throw new ArgumentNullException(nameof(openBehaviorType));
        if (!openBehaviorType.IsGenericType)
            throw new InvalidOperationException($"{openBehaviorType.Name} must be generic");

        var implementedGenericInterfaces = openBehaviorType.GetInterfaces().Where(i => i.IsGenericType)
            .Select(i => i.GetGenericTypeDefinition());
        var implementedOpenBehaviorInterfaces =
            new HashSet<Type>(implementedGenericInterfaces.Where(i => i == typeof(IRequestPreProcessor<>)));

        if (implementedOpenBehaviorInterfaces.Count == 0)
            throw new InvalidOperationException(
                $"{openBehaviorType.Name} must implement {typeof(IRequestPreProcessor<>).FullName}");

        foreach (var openBehaviorInterface in implementedOpenBehaviorInterfaces)
            RequestPreProcessorsToRegister.Add(new ServiceDescriptor(openBehaviorInterface, openBehaviorType,
                serviceLifetime));

        return this;
    }

    /// <summary>
    ///     Registers a closed request post-processor mapping between interface and implementation.
    /// </summary>
    /// <typeparam name="TServiceType">Closed request post-processor interface.</typeparam>
    /// <typeparam name="TImplementationType">Closed request post-processor implementation.</typeparam>
    /// <param name="serviceLifetime">Optional lifetime override.</param>
    /// <returns>The same configuration instance for fluent chaining.</returns>
    public UmlamuliServiceConfiguration AddRequestPostProcessor<TServiceType, TImplementationType>(
        ServiceLifetime serviceLifetime = ServiceLifetime.Transient)
        => AddRequestPostProcessor(typeof(TServiceType), typeof(TImplementationType), serviceLifetime);

    /// <summary>
    ///     Registers a closed request post-processor mapping between interface and implementation.
    /// </summary>
    /// <param name="serviceType">Closed request post-processor interface type.</param>
    /// <param name="implementationType">Closed request post-processor implementation type.</param>
    /// <param name="serviceLifetime">Optional lifetime override.</param>
    /// <returns>The same configuration instance for fluent chaining.</returns>
    public UmlamuliServiceConfiguration AddRequestPostProcessor(Type serviceType, Type implementationType,
        ServiceLifetime serviceLifetime = ServiceLifetime.Transient)
    {
        RequestPostProcessorsToRegister.Add(new ServiceDescriptor(serviceType, implementationType, serviceLifetime));
        return this;
    }

    /// <summary>
    ///     Registers a closed request post-processor implementation against each closed
    ///     <see cref="IRequestPostProcessor{TRequest,TResponse}" /> interface it implements.
    /// </summary>
    /// <typeparam name="TImplementationType">Closed request post-processor implementation type.</typeparam>
    /// <param name="serviceLifetime">Optional lifetime override.</param>
    /// <returns>The same configuration instance for fluent chaining.</returns>
    public UmlamuliServiceConfiguration AddRequestPostProcessor<TImplementationType>(
        ServiceLifetime serviceLifetime = ServiceLifetime.Transient)
        => AddRequestPostProcessor(typeof(TImplementationType), serviceLifetime);

    /// <summary>
    ///     Registers a closed request post-processor implementation against each closed
    ///     <see cref="IRequestPostProcessor{TRequest,TResponse}" /> interface it implements.
    /// </summary>
    /// <param name="implementationType">Closed request post-processor implementation type.</param>
    /// <param name="serviceLifetime">Optional lifetime override.</param>
    /// <returns>The same configuration instance for fluent chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="implementationType" /> is null.</exception>
    /// <exception cref="InvalidOperationException">
    ///     Thrown when the type does not implement any request post-processor
    ///     interfaces.
    /// </exception>
    public UmlamuliServiceConfiguration AddRequestPostProcessor(Type implementationType,
        ServiceLifetime serviceLifetime = ServiceLifetime.Transient)
    {
        if (implementationType == null) throw new ArgumentNullException(nameof(implementationType));
        var implementedGenericInterfaces =
            implementationType.FindInterfacesThatClose(typeof(IRequestPostProcessor<,>)).ToList();

        if (implementedGenericInterfaces.Count == 0)
            throw new InvalidOperationException(
                $"{implementationType.Name} must implement {typeof(IRequestPostProcessor<,>).FullName}");

        foreach (var implementedPostProcessorType in implementedGenericInterfaces)
            RequestPostProcessorsToRegister.Add(new ServiceDescriptor(implementedPostProcessorType, implementationType,
                serviceLifetime));
        return this;
    }

    /// <summary>
    ///     Registers an open generic request post-processor type against the open
    ///     <see cref="IRequestPostProcessor{TRequest,TResponse}" /> interface.
    /// </summary>
    /// <param name="openBehaviorType">Open generic request post-processor type.</param>
    /// <param name="serviceLifetime">Optional lifetime override.</param>
    /// <returns>The same configuration instance for fluent chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="openBehaviorType" /> is null.</exception>
    /// <exception cref="InvalidOperationException">
    ///     Thrown when the type is not generic or does not implement the required
    ///     interface.
    /// </exception>
    public UmlamuliServiceConfiguration AddOpenRequestPostProcessor(Type openBehaviorType,
        ServiceLifetime serviceLifetime = ServiceLifetime.Transient)
    {
        if (openBehaviorType == null) throw new ArgumentNullException(nameof(openBehaviorType));
        if (!openBehaviorType.IsGenericType)
            throw new InvalidOperationException($"{openBehaviorType.Name} must be generic");

        var implementedGenericInterfaces = openBehaviorType.GetInterfaces().Where(i => i.IsGenericType)
            .Select(i => i.GetGenericTypeDefinition());
        var implementedOpenBehaviorInterfaces =
            new HashSet<Type>(implementedGenericInterfaces.Where(i => i == typeof(IRequestPostProcessor<,>)));

        if (implementedOpenBehaviorInterfaces.Count == 0)
            throw new InvalidOperationException(
                $"{openBehaviorType.Name} must implement {typeof(IRequestPostProcessor<,>).FullName}");

        foreach (var openBehaviorInterface in implementedOpenBehaviorInterfaces)
            RequestPostProcessorsToRegister.Add(new ServiceDescriptor(openBehaviorInterface, openBehaviorType,
                serviceLifetime));

        return this;
    }
}