using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Umlamuli.Pipeline;

namespace Umlamuli.Registration;

/// <summary>
///     Provides assembly scanning and service registration helpers for Umlamuli (MediatR-like) components,
///     including handlers, processors, behaviors, and notification publishers.
/// </summary>
/// <remarks>
///     This registrar supports both closed and open generic handler discovery and enforces optional limits
///     on generic handler expansion to prevent excessive registrations.
///     Use <see cref="SetGenericRequestHandlerRegistrationLimitations" /> prior to invoking
///     <see cref="AddUmlamuliClassesWithTimeout" /> or <see cref="AddUmlamuliClasses" /> if constraints are desired.
/// </remarks>
public static class ServiceRegistrar
{
    private static int _maxGenericTypeParameters;
    private static int _maxTypesClosing;
    private static int _maxGenericTypeRegistrations;
    private static int _registrationTimeout;

    /// <summary>
    ///     Applies optional constraints governing how generic request handler registrations are expanded.
    /// </summary>
    /// <param name="configuration">Configuration source for constraint values.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="configuration" /> is null.</exception>
    /// <remarks>
    ///     Limits:
    ///     <list type="bullet">
    ///         <item>
    ///             <description><see cref="UmlamuliServiceConfiguration.MaxGenericTypeParameters" /> caps generic arity.</description>
    ///         </item>
    ///         <item>
    ///             <description>
    ///                 <see cref="UmlamuliServiceConfiguration.MaxTypesClosing" /> caps candidate closing types per
    ///                 parameter.
    ///             </description>
    ///         </item>
    ///         <item>
    ///             <description>
    ///                 <see cref="UmlamuliServiceConfiguration.MaxGenericTypeRegistrations" /> caps total expansion
    ///                 count.
    ///             </description>
    ///         </item>
    ///         <item>
    ///             <description>
    ///                 <see cref="UmlamuliServiceConfiguration.RegistrationTimeout" /> sets millisecond timeout for
    ///                 expansion (used by <see cref="AddUmlamuliClassesWithTimeout" />).
    ///             </description>
    ///         </item>
    ///     </list>
    ///     A value of 0 for any limit disables that constraint.
    /// </remarks>
    public static void SetGenericRequestHandlerRegistrationLimitations(UmlamuliServiceConfiguration configuration)
    {
        if (configuration == null) throw new ArgumentNullException(nameof(configuration));
        _maxGenericTypeParameters = configuration.MaxGenericTypeParameters;
        _maxTypesClosing = configuration.MaxTypesClosing;
        _maxGenericTypeRegistrations = configuration.MaxGenericTypeRegistrations;
        _registrationTimeout = configuration.RegistrationTimeout;
    }

    /// <summary>
    ///     Scans configured assemblies and registers Umlamuli components enforcing a timeout for generic handler expansion.
    /// </summary>
    /// <param name="services">The DI service collection to append registrations to.</param>
    /// <param name="configuration">The mediator service configuration.</param>
    /// <exception cref="TimeoutException">
    ///     Thrown when the generic handler expansion exceeds the configured timeout (see
    ///     <see cref="SetGenericRequestHandlerRegistrationLimitations" />).
    /// </exception>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="configuration" /> is null.</exception>
    public static void AddUmlamuliClassesWithTimeout(IServiceCollection services,
        UmlamuliServiceConfiguration configuration)
    {
        using var cts = new CancellationTokenSource(_registrationTimeout);
        try
        {
            AddUmlamuliClasses(services, configuration, cts.Token);
        }
        catch (OperationCanceledException)
        {
            throw new TimeoutException("The generic handler registration process timed out.");
        }
    }

    /// <summary>
    ///     Scans assemblies declared in <paramref name="configuration" /> and registers handlers, processors, and behaviors.
    /// </summary>
    /// <param name="services">The DI service collection target.</param>
    /// <param name="configuration">Runtime registration configuration.</param>
    /// <param name="cancellationToken">Optional token to cancel long-running generic expansion.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="configuration" /> is null.</exception>
    /// <remarks>
    ///     Registration order:
    ///     <list type="number">
    ///         <item>
    ///             Request handlers (<see cref="IRequestHandler{TRequest,TResponse}" />,
    ///             <see cref="IRequestHandler{TRequest}" />)
    ///         </item>
    ///         <item>Notification handlers (<see cref="INotificationHandler{TNotification}" />)</item>
    ///         <item>Stream request handlers (<see cref="IStreamRequestHandler{TRequest,TResponse}" />)</item>
    ///         <item>Exception handlers / actions</item>
    ///         <item>
    ///             Optionally pre- / post-processors if
    ///             <see cref="UmlamuliServiceConfiguration.AutoRegisterRequestProcessors" /> is true.
    ///         </item>
    ///     </list>
    ///     Open generic handlers are also registered so they can be closed later via the container.
    /// </remarks>
    public static void AddUmlamuliClasses(IServiceCollection services, UmlamuliServiceConfiguration configuration,
        CancellationToken cancellationToken = default)
    {
        if (configuration == null) throw new ArgumentNullException(nameof(configuration));

        var assembliesToScan = configuration.AssembliesToRegister.Distinct().ToArray();

        ConnectImplementationsToTypesClosing(typeof(IRequestHandler<,>), services, assembliesToScan, false,
            configuration, cancellationToken);
        ConnectImplementationsToTypesClosing(typeof(IRequestHandler<>), services, assembliesToScan, false,
            configuration, cancellationToken);
        ConnectImplementationsToTypesClosing(typeof(INotificationHandler<>), services, assembliesToScan, true,
            configuration, cancellationToken);
        ConnectImplementationsToTypesClosing(typeof(IStreamRequestHandler<,>), services, assembliesToScan, false,
            configuration, cancellationToken);
        ConnectImplementationsToTypesClosing(typeof(IRequestExceptionHandler<,,>), services, assembliesToScan, true,
            configuration, cancellationToken);
        ConnectImplementationsToTypesClosing(typeof(IRequestExceptionAction<,>), services, assembliesToScan, true,
            configuration, cancellationToken);

        if (configuration.AutoRegisterRequestProcessors)
        {
            ConnectImplementationsToTypesClosing(typeof(IRequestPreProcessor<>), services, assembliesToScan, true,
                configuration, cancellationToken);
            ConnectImplementationsToTypesClosing(typeof(IRequestPostProcessor<,>), services, assembliesToScan, true,
                configuration, cancellationToken);
        }

        var multiOpenInterfaces = new List<Type>
        {
            typeof(INotificationHandler<>),
            typeof(IRequestExceptionHandler<,,>),
            typeof(IRequestExceptionAction<,>)
        };

        if (configuration.AutoRegisterRequestProcessors)
        {
            multiOpenInterfaces.Add(typeof(IRequestPreProcessor<>));
            multiOpenInterfaces.Add(typeof(IRequestPostProcessor<,>));
        }

        foreach (var multiOpenInterface in multiOpenInterfaces)
        {
            int arity = multiOpenInterface.GetGenericArguments().Length;

            var concretions = assembliesToScan
                .SelectMany(a => a.DefinedTypes)
                .Where(type => type.FindInterfacesThatClose(multiOpenInterface).Any())
                .Where(type => type.IsConcrete() && type.IsOpenGeneric())
                .Where(type => type.GetGenericArguments().Length == arity)
                .Where(configuration.TypeEvaluator)
                .ToList();

            foreach (var type in concretions) services.AddTransient(multiOpenInterface, type);
        }
    }

    /// <summary>
    ///     Discovers and connects concrete implementation types to service interfaces that close a specified open generic
    ///     mediator interface.
    /// </summary>
    /// <param name="openRequestInterface">The open generic mediator interface (e.g., typeof(IRequestHandler&lt;,&gt;)).</param>
    /// <param name="services">DI target collection.</param>
    /// <param name="assembliesToScan">Assemblies to search.</param>
    /// <param name="addIfAlreadyExists">If true allows multiple registrations; otherwise uses TryAdd semantics.</param>
    /// <param name="configuration">Registration configuration (filters and options).</param>
    /// <param name="cancellationToken">Token used during potentially long-running generic expansion.</param>
    private static void ConnectImplementationsToTypesClosing(Type openRequestInterface,
        IServiceCollection services,
        IEnumerable<Assembly> assembliesToScan,
        bool addIfAlreadyExists,
        UmlamuliServiceConfiguration configuration,
        CancellationToken cancellationToken = default)
    {
        var concretions = new List<Type>();
        var interfaces = new List<Type>();
        var genericConcretions = new List<Type>();
        var genericInterfaces = new List<Type>();

        var types = assembliesToScan
            .SelectMany(a => a.DefinedTypes)
            .Where(t => !t.ContainsGenericParameters || configuration.RegisterGenericHandlers)
            .Where(t => t.IsConcrete() && t.FindInterfacesThatClose(openRequestInterface).Any())
            .Where(configuration.TypeEvaluator)
            .ToList();

        foreach (var type in types)
        {
            var interfaceTypes = type.FindInterfacesThatClose(openRequestInterface).ToArray();

            if (!type.IsOpenGeneric())
            {
                concretions.Add(type);

                foreach (var interfaceType in interfaceTypes) interfaces.Fill(interfaceType);
            }
            else
            {
                genericConcretions.Add(type);
                foreach (var interfaceType in interfaceTypes) genericInterfaces.Fill(interfaceType);
            }
        }

        foreach (var @interface in interfaces)
        {
            var exactMatches = concretions.Where(x => x.CanBeCastTo(@interface)).ToList();
            if (addIfAlreadyExists)
            {
                foreach (var type in exactMatches) services.AddTransient(@interface, type);
            }
            else
            {
                if (exactMatches.Count > 1) exactMatches.RemoveAll(m => !IsMatchingWithInterface(m, @interface));

                foreach (var type in exactMatches) services.TryAddTransient(@interface, type);
            }

            if (!@interface.IsOpenGeneric()) AddConcretionsThatCouldBeClosed(@interface, concretions, services);
        }

        foreach (var @interface in genericInterfaces)
        {
            var exactMatches = genericConcretions.Where(x => x.CanBeCastTo(@interface)).ToList();
            AddAllConcretionsThatClose(@interface, exactMatches, services, assembliesToScan, cancellationToken);
        }
    }

    /// <summary>
    ///     Determines if a handler implementation type matches a handler interface by comparing generic arguments.
    /// </summary>
    /// <param name="handlerType">Implementation or interface type.</param>
    /// <param name="handlerInterface">The interface to test against.</param>
    /// <returns><c>true</c> if generic arguments align; otherwise <c>false</c>.</returns>
    private static bool IsMatchingWithInterface(Type? handlerType, Type handlerInterface)
    {
        if (handlerType == null || handlerInterface == null) return false;

        if (handlerType.IsInterface)
        {
            if (handlerType.GenericTypeArguments.SequenceEqual(handlerInterface.GenericTypeArguments)) return true;
        }
        else
        {
            return IsMatchingWithInterface(handlerType.GetInterface(handlerInterface.Name), handlerInterface);
        }

        return false;
    }

    /// <summary>
    ///     Attempts to close open generic concrete types to a closed interface and registers them when possible.
    /// </summary>
    /// <param name="interface">Closed interface type.</param>
    /// <param name="concretions">All concrete types discovered.</param>
    /// <param name="services">DI collection.</param>
    private static void AddConcretionsThatCouldBeClosed(Type @interface, List<Type> concretions,
        IServiceCollection services)
    {
        foreach (var type in concretions
                     .Where(x => x.IsOpenGeneric() && x.CouldCloseTo(@interface)))
            try
            {
                services.TryAddTransient(@interface, type.MakeGenericType(@interface.GenericTypeArguments));
            }
            catch (Exception)
            {
                // Swallow exceptions that arise from invalid generic argument alignment.
            }
    }

    /// <summary>
    ///     Builds the service (closed mediator interface) and implementation types required for registration
    ///     given an open generic handler interface and implementation.
    /// </summary>
    /// <param name="openRequestHandlerInterface">The open handler interface (e.g., IRequestHandler&lt;,&gt;).</param>
    /// <param name="concreteGenericTRequest">Closed generic request type.</param>
    /// <param name="openRequestHandlerImplementation">Open generic handler implementation type.</param>
    /// <returns>A tuple containing the closed service type and its corresponding closed implementation type.</returns>
    private static (Type Service, Type Implementation) GetConcreteRegistrationTypes(Type openRequestHandlerInterface,
        Type concreteGenericTRequest, Type openRequestHandlerImplementation)
    {
        var closingTypes = concreteGenericTRequest.GetGenericArguments();

        var concreteTResponse = concreteGenericTRequest.GetInterfaces()
            .FirstOrDefault(x => x.IsGenericType && x.GetGenericTypeDefinition() == typeof(IRequest<>))
            ?.GetGenericArguments()
            .FirstOrDefault();

        var typeDefinition = openRequestHandlerInterface.GetGenericTypeDefinition();

        var serviceType = concreteTResponse != null
            ? typeDefinition.MakeGenericType(concreteGenericTRequest, concreteTResponse)
            : typeDefinition.MakeGenericType(concreteGenericTRequest);

        return (serviceType, openRequestHandlerImplementation.MakeGenericType(closingTypes));
    }

    /// <summary>
    ///     Produces all concrete request types that satisfy the generic parameter constraints of an open handler
    ///     implementation.
    /// </summary>
    /// <param name="openRequestHandlerInterface">
    ///     Closed handler interface being processed (already bound to a request generic
    ///     definition).
    /// </param>
    /// <param name="openRequestHandlerImplementation">Open handler implementation type.</param>
    /// <param name="assembliesToScan">Assemblies to search for candidate types that satisfy constraints.</param>
    /// <param name="cancellationToken">Token to cancel enumeration.</param>
    /// <returns>A list of closed request types or null when the request generic is not yet closed.</returns>
    private static List<Type>? GetConcreteRequestTypes(Type openRequestHandlerInterface,
        Type openRequestHandlerImplementation, IEnumerable<Assembly> assembliesToScan,
        CancellationToken cancellationToken)
    {
        // request generic type constraints
        var constraintsForEachParameter = openRequestHandlerImplementation
            .GetGenericArguments()
            .Select(x => x.GetGenericParameterConstraints())
            .ToList();

        var typesThatCanCloseForEachParameter = constraintsForEachParameter
            .Select(constraints => assembliesToScan
                .SelectMany(assembly => assembly.GetTypes())
                .Where(type =>
                    type is { IsClass: true, IsAbstract: false } &&
                    constraints.All(constraint => constraint.IsAssignableFrom(type))).ToList()
            ).ToList();

        var requestType = openRequestHandlerInterface.GenericTypeArguments.First();

        if (requestType.IsGenericParameter)
            return null;

        var requestGenericTypeDefinition = requestType.GetGenericTypeDefinition();

        var combinations = GenerateCombinations(requestType, typesThatCanCloseForEachParameter, 0, cancellationToken);

        return combinations.Select(types => requestGenericTypeDefinition.MakeGenericType(types.ToArray())).ToList();
    }

    /// <summary>
    ///     Generates all Cartesian product combinations from multiple candidate type lists respecting configured limits.
    /// </summary>
    /// <param name="requestType">The original generic request type used for error reporting.</param>
    /// <param name="lists">Lists of candidate closing types per generic parameter.</param>
    /// <param name="depth">Current recursion depth (internal use).</param>
    /// <param name="cancellationToken">Token to cancel expansion mid-way.</param>
    /// <returns>All discovered combinations (each combination is an ordered list aligned with generic parameters).</returns>
    /// <exception cref="ArgumentNullException">
    ///     Thrown when <paramref name="requestType" /> or <paramref name="lists" /> is
    ///     null.
    /// </exception>
    /// <exception cref="ArgumentException">
    ///     Thrown when any configured limit (<see cref="UmlamuliServiceConfiguration.MaxGenericTypeParameters" />,
    ///     <see cref="UmlamuliServiceConfiguration.MaxTypesClosing" />,
    ///     <see cref="UmlamuliServiceConfiguration.MaxGenericTypeRegistrations" />) is exceeded.
    /// </exception>
    public static List<List<Type>> GenerateCombinations(Type requestType, List<List<Type>> lists, int depth = 0,
        CancellationToken cancellationToken = default)
    {
        if (requestType == null) throw new ArgumentNullException(nameof(requestType));
        if (lists == null) throw new ArgumentNullException(nameof(lists));
        if (depth == 0)
        {
            // Initial checks
            if (_maxGenericTypeParameters > 0 && lists.Count > _maxGenericTypeParameters)
                throw new ArgumentException(
                    $"Error registering the generic type: {requestType.FullName}. The number of generic type parameters exceeds the maximum allowed ({_maxGenericTypeParameters}).");

            foreach (var list in lists)
                if (_maxTypesClosing > 0 && list.Count > _maxTypesClosing)
                    throw new ArgumentException(
                        $"Error registering the generic type: {requestType.FullName}. One of the generic type parameter's count of types that can close exceeds the maximum length allowed ({_maxTypesClosing}).");

            // Calculate the total number of combinations
            long totalCombinations = 1;
            foreach (var list in lists)
            {
                totalCombinations *= list.Count;
                if (_maxGenericTypeParameters > 0 && totalCombinations > _maxGenericTypeRegistrations)
                    throw new ArgumentException(
                        $"Error registering the generic type: {requestType.FullName}. The total number of generic type registrations exceeds the maximum allowed ({_maxGenericTypeRegistrations}).");
            }
        }

        if (depth >= lists.Count)
            return [new()];

        cancellationToken.ThrowIfCancellationRequested();

        var currentList = lists[depth];
        var childCombinations = GenerateCombinations(requestType, lists, depth + 1, cancellationToken);
        var combinations = new List<List<Type>>();

        foreach (var item in currentList)
        foreach (var childCombination in childCombinations)
        {
            var currentCombination = new List<Type> { item };
            currentCombination.AddRange(childCombination);
            combinations.Add(currentCombination);
        }

        return combinations;
    }

    /// <summary>
    ///     Registers all closed handler implementations created by closing open generic handler implementations against
    ///     discovered request types.
    /// </summary>
    /// <param name="openRequestInterface">An open generic handler interface.</param>
    /// <param name="concretions">List of open generic handler implementation types that can be closed.</param>
    /// <param name="services">DI service collection.</param>
    /// <param name="assembliesToScan">Assemblies to search for closing types.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    private static void AddAllConcretionsThatClose(Type openRequestInterface, List<Type> concretions,
        IServiceCollection services, IEnumerable<Assembly> assembliesToScan, CancellationToken cancellationToken)
    {
        foreach (var concretion in concretions)
        {
            var concreteRequests =
                GetConcreteRequestTypes(openRequestInterface, concretion, assembliesToScan, cancellationToken);

            if (concreteRequests is null)
                continue;

            var registrationTypes = concreteRequests
                .Select(concreteRequest =>
                    GetConcreteRegistrationTypes(openRequestInterface, concreteRequest, concretion));

            foreach (var (service, implementation) in registrationTypes)
            {
                cancellationToken.ThrowIfCancellationRequested();
                services.AddTransient(service, implementation);
            }
        }
    }

    /// <summary>
    ///     Determines whether an open generic concretion could be closed to match a provided closed interface.
    /// </summary>
    /// <param name="openConcretion">Open generic implementation type.</param>
    /// <param name="closedInterface">Closed generic interface type.</param>
    /// <returns><c>true</c> if closure is possible; otherwise <c>false</c>.</returns>
    internal static bool CouldCloseTo(this Type openConcretion, Type closedInterface)
    {
        var openInterface = closedInterface.GetGenericTypeDefinition();
        var arguments = closedInterface.GenericTypeArguments;

        var concreteArguments = openConcretion.GenericTypeArguments;
        return arguments.Length == concreteArguments.Length && openConcretion.CanBeCastTo(openInterface);
    }

    /// <summary>
    ///     Determines whether a type can be cast (assigned) to another type (interface or base class).
    /// </summary>
    /// <param name="pluggedType">Source type.</param>
    /// <param name="pluginType">Target type.</param>
    /// <returns><c>true</c> if assignable; otherwise <c>false</c>.</returns>
    private static bool CanBeCastTo(this Type pluggedType, Type pluginType)
    {
        if (pluggedType == null) return false;

        if (pluggedType == pluginType) return true;

        return pluginType.IsAssignableFrom(pluggedType);
    }

    /// <summary>
    ///     Indicates whether a type is an open generic (definition or contains unbound generic parameters).
    /// </summary>
    /// <param name="type">Type to evaluate.</param>
    /// <returns><c>true</c> if open generic; otherwise <c>false</c>.</returns>
    private static bool IsOpenGeneric(this Type type) => type.IsGenericTypeDefinition || type.ContainsGenericParameters;

    /// <summary>
    ///     Finds interfaces implemented by <paramref name="pluggedType" /> that close the specified open generic interface
    ///     template.
    /// </summary>
    /// <param name="pluggedType">Concrete type to inspect.</param>
    /// <param name="templateType">Open generic interface or base type.</param>
    /// <returns>Distinct sequence of closed interface types matching the template.</returns>
    internal static IEnumerable<Type> FindInterfacesThatClose(this Type pluggedType, Type templateType) =>
        FindInterfacesThatClosesCore(pluggedType, templateType).Distinct();

    /// <summary>
    ///     Core recursive search for closed interfaces matching a template.
    /// </summary>
    /// <param name="pluggedType">Concrete type.</param>
    /// <param name="templateType">Open generic template interface or base type.</param>
    /// <returns>Enumeration of matching closed interfaces.</returns>
    private static IEnumerable<Type> FindInterfacesThatClosesCore(Type pluggedType, Type templateType)
    {
        if (pluggedType == null) yield break;

        if (!pluggedType.IsConcrete()) yield break;

        if (templateType.IsInterface)
            foreach (
                var interfaceType in
                pluggedType.GetInterfaces()
                    .Where(type => type.IsGenericType && (type.GetGenericTypeDefinition() == templateType)))
                yield return interfaceType;
        else if (pluggedType.BaseType!.IsGenericType &&
                 (pluggedType.BaseType!.GetGenericTypeDefinition() == templateType))
            yield return pluggedType.BaseType!;

        if (pluggedType.BaseType == typeof(object)) yield break;

        foreach (var interfaceType in FindInterfacesThatClosesCore(pluggedType.BaseType!, templateType))
            yield return interfaceType;
    }

    /// <summary>
    ///     Determines whether a type is a concrete (non-abstract, non-interface) implementation.
    /// </summary>
    /// <param name="type">Type to inspect.</param>
    /// <returns><c>true</c> if concrete; otherwise <c>false</c>.</returns>
    private static bool IsConcrete(this Type type) => type is { IsAbstract: false, IsInterface: false };

    /// <summary>
    ///     Adds a value to a list only if it is not already present.
    /// </summary>
    /// <typeparam name="T">Element type.</typeparam>
    /// <param name="list">Target list.</param>
    /// <param name="value">Value to append if absent.</param>
    private static void Fill<T>(this IList<T> list, T value)
    {
        if (list.Contains(value)) return;
        list.Add(value);
    }

    /// <summary>
    ///     Registers core mediator services (IMediator, ISender, IPublisher, notification publisher strategy, behaviors,
    ///     processors) required for Umlamuli operation.
    /// </summary>
    /// <param name="services">DI service collection.</param>
    /// <param name="serviceConfiguration">Configuration describing service lifetimes and ordered behaviors.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="serviceConfiguration" /> is null.</exception>
    /// <remarks>
    ///     Behavior registration order is important for pipeline execution. Uses TryAdd semantics to avoid overriding prior
    ///     explicit registrations.
    ///     Exception processor and action behaviors are ordered based on
    ///     <see cref="UmlamuliServiceConfiguration.RequestExceptionActionProcessorStrategy" />.
    /// </remarks>
    public static void AddRequiredServices(IServiceCollection services,
        UmlamuliServiceConfiguration serviceConfiguration)
    {
        if (serviceConfiguration == null) throw new ArgumentNullException(nameof(serviceConfiguration));
        // Use TryAdd, so any existing ServiceFactory/IMediator registration doesn't get overridden
        services.TryAdd(new ServiceDescriptor(typeof(IMediator), serviceConfiguration.MediatorImplementationType,
            serviceConfiguration.Lifetime));
        services.TryAdd(new ServiceDescriptor(typeof(ISender), sp => sp.GetRequiredService<IMediator>(),
            serviceConfiguration.Lifetime));
        services.TryAdd(new ServiceDescriptor(typeof(IPublisher), sp => sp.GetRequiredService<IMediator>(),
            serviceConfiguration.Lifetime));

        var notificationPublisherServiceDescriptor = serviceConfiguration.NotificationPublisherType != null
            ? new ServiceDescriptor(typeof(INotificationPublisher), serviceConfiguration.NotificationPublisherType,
                serviceConfiguration.Lifetime)
            : new ServiceDescriptor(typeof(INotificationPublisher), serviceConfiguration.NotificationPublisher);

        services.TryAdd(notificationPublisherServiceDescriptor);

        // Register pre-processors, then post-processors, then behaviors
        if (serviceConfiguration.RequestExceptionActionProcessorStrategy ==
            RequestExceptionActionProcessorStrategy.ApplyForUnhandledExceptions)
        {
            RegisterBehaviorIfImplementationsExist(services, typeof(RequestExceptionActionProcessorBehavior<,>),
                typeof(IRequestExceptionAction<,>));
            RegisterBehaviorIfImplementationsExist(services, typeof(RequestExceptionProcessorBehavior<,>),
                typeof(IRequestExceptionHandler<,,>));
        }
        else
        {
            RegisterBehaviorIfImplementationsExist(services, typeof(RequestExceptionProcessorBehavior<,>),
                typeof(IRequestExceptionHandler<,,>));
            RegisterBehaviorIfImplementationsExist(services, typeof(RequestExceptionActionProcessorBehavior<,>),
                typeof(IRequestExceptionAction<,>));
        }

        if (serviceConfiguration.RequestPreProcessorsToRegister.Any())
        {
            services.TryAddEnumerable(new ServiceDescriptor(typeof(IPipelineBehavior<,>),
                typeof(RequestPreProcessorBehavior<,>), ServiceLifetime.Transient));
            services.TryAddEnumerable(serviceConfiguration.RequestPreProcessorsToRegister);
        }

        if (serviceConfiguration.RequestPostProcessorsToRegister.Any())
        {
            services.TryAddEnumerable(new ServiceDescriptor(typeof(IPipelineBehavior<,>),
                typeof(RequestPostProcessorBehavior<,>), ServiceLifetime.Transient));
            services.TryAddEnumerable(serviceConfiguration.RequestPostProcessorsToRegister);
        }

        foreach (var serviceDescriptor in serviceConfiguration.BehaviorsToRegister)
            services.TryAddEnumerable(serviceDescriptor);

        foreach (var serviceDescriptor in serviceConfiguration.StreamBehaviorsToRegister)
            services.TryAddEnumerable(serviceDescriptor);
    }

    /// <summary>
    ///     Adds a pipeline behavior registration only if implementations of a sub-behavior type have already been registered.
    /// </summary>
    /// <param name="services">DI collection.</param>
    /// <param name="behaviorType">
    ///     Open generic behavior implementation type (e.g., RequestExceptionProcessorBehavior&lt;,&gt;
    ///     ).
    /// </param>
    /// <param name="subBehaviorType">
    ///     Sub behavior interface definition used as presence indicator (e.g.,
    ///     IRequestExceptionHandler&lt;,,&gt;).
    /// </param>
    private static void RegisterBehaviorIfImplementationsExist(IServiceCollection services, Type behaviorType,
        Type subBehaviorType)
    {
        bool hasAnyRegistrationsOfSubBehaviorType = services
            .Where(service => !service.IsKeyedService)
            .Select(service => service.ImplementationType)
            .OfType<Type>()
            .SelectMany(type => type.GetInterfaces())
            .Where(type => type.IsGenericType)
            .Select(type => type.GetGenericTypeDefinition())
            .Any(type => type == subBehaviorType);

        if (hasAnyRegistrationsOfSubBehaviorType)
            services.TryAddEnumerable(new ServiceDescriptor(typeof(IPipelineBehavior<,>), behaviorType,
                ServiceLifetime.Transient));
    }
}