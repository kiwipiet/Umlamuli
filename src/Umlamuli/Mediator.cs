using System.Collections.Concurrent;
using Umlamuli.NotificationPublishers;
using Umlamuli.Wrappers;

namespace Umlamuli;

/// <summary>
///     Default mediator implementation relying on single- and multi-instance delegates for resolving handlers.
///     Provides methods to send requests, publish notifications, and create async streams
///     by locating and invoking the appropriate handlers via service provider resolution.
/// </summary>
/// <remarks>
///     This implementation caches handler wrapper instances per request/notification type for performance.
///     The cache is shared across mediator instances and implemented with thread-safe
///     <see cref="ConcurrentDictionary{TKey,TValue}" />.
/// </remarks>
public class Mediator : IMediator
{
    /// <summary>
    ///     Cache of request handler wrappers keyed by the request type.
    /// </summary>
    private static readonly ConcurrentDictionary<Type, RequestHandlerBase> s_requestHandlers = new();

    /// <summary>
    ///     Cache of notification handler wrappers keyed by the notification type.
    /// </summary>
    private static readonly ConcurrentDictionary<Type, NotificationHandlerWrapper> s_notificationHandlers = new();

    /// <summary>
    ///     Cache of stream request handler wrappers keyed by the request type.
    /// </summary>
    private static readonly ConcurrentDictionary<Type, StreamRequestHandlerBase> s_streamRequestHandlers = new();

    private readonly INotificationPublisher _publisher;
    private readonly IServiceProvider _serviceProvider;

    /// <summary>
    ///     Initializes a new instance of the <see cref="Mediator" /> class.
    /// </summary>
    /// <param name="serviceProvider">Service provider. Can be a scoped or root provider.</param>
    /// <remarks>
    ///     Uses <see cref="ForeachAwaitPublisher" /> as the default notification publisher.
    /// </remarks>
    public Mediator(IServiceProvider serviceProvider)
        : this(serviceProvider, new ForeachAwaitPublisher())
    {
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="Mediator" /> class.
    /// </summary>
    /// <param name="serviceProvider">Service provider. Can be a scoped or root provider.</param>
    /// <param name="publisher">Notification publisher. Defaults to <see cref="ForeachAwaitPublisher" />.</param>
    /// <exception cref="ArgumentNullException">
    ///     Thrown when <paramref name="serviceProvider" /> or
    ///     <paramref name="publisher" /> is <c>null</c>.
    /// </exception>
    public Mediator(IServiceProvider serviceProvider, INotificationPublisher publisher)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _publisher = publisher ?? throw new ArgumentNullException(nameof(publisher));
    }

    /// <summary>
    ///     Asynchronously sends a request to a single handler and returns its response.
    /// </summary>
    /// <typeparam name="TResponse">The response type returned by the handler.</typeparam>
    /// <param name="request">The request object implementing <see cref="IRequest{TResponse}" />.</param>
    /// <param name="cancellationToken">Optional <see cref="CancellationToken" /> used to cancel the operation.</param>
    /// <returns>A task representing the send operation with the handler's response.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="request" /> is <c>null</c>.</exception>
    public Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
    {
        if (request == null) throw new ArgumentNullException(nameof(request));

        var handler = (RequestHandlerWrapper<TResponse>)s_requestHandlers.GetOrAdd(request.GetType(),
            static requestType =>
            {
                var wrapperType = typeof(RequestHandlerWrapperImpl<,>).MakeGenericType(requestType, typeof(TResponse));
                object wrapper = Activator.CreateInstance(wrapperType) ??
                                 throw new InvalidOperationException(
                                     $"Could not create wrapper type for {requestType}");
                return (RequestHandlerBase)wrapper;
            });

        return handler.Handle(request, _serviceProvider, cancellationToken);
    }

    /// <summary>
    ///     Asynchronously sends a request to a single handler that does not produce a response.
    /// </summary>
    /// <typeparam name="TRequest">The request type implementing <see cref="IRequest" />.</typeparam>
    /// <param name="request">The request object.</param>
    /// <param name="cancellationToken">Optional <see cref="CancellationToken" /> used to cancel the operation.</param>
    /// <returns>A task representing the send operation.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="request" /> is <c>null</c>.</exception>
    public Task Send<TRequest>(TRequest request, CancellationToken cancellationToken = default)
        where TRequest : IRequest
    {
        if (request == null) throw new ArgumentNullException(nameof(request));

        var handler = (RequestHandlerWrapper)s_requestHandlers.GetOrAdd(request.GetType(), static requestType =>
        {
            var wrapperType = typeof(RequestHandlerWrapperImpl<>).MakeGenericType(requestType);
            object wrapper = Activator.CreateInstance(wrapperType) ??
                             throw new InvalidOperationException($"Could not create wrapper type for {requestType}");
            return (RequestHandlerBase)wrapper;
        });

        return handler.Handle(request, _serviceProvider, cancellationToken);
    }

    /// <summary>
    ///     Asynchronously sends an object request to a single handler via dynamic dispatch and returns the type-erased
    ///     response.
    /// </summary>
    /// <param name="request">The request object implementing <see cref="IRequest" /> or <see cref="IRequest{TResponse}" />.</param>
    /// <param name="cancellationToken">Optional <see cref="CancellationToken" /> used to cancel the operation.</param>
    /// <returns>
    ///     A task representing the send operation. The task result contains the handler response if any; otherwise
    ///     <c>null</c>.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="request" /> is <c>null</c>.</exception>
    /// <exception cref="ArgumentException">
    ///     Thrown when the <paramref name="request" /> type does not implement
    ///     <see cref="IRequest" />.
    /// </exception>
    public Task<object?> Send(object request, CancellationToken cancellationToken = default)
    {
        if (request == null) throw new ArgumentNullException(nameof(request));

        var handler = s_requestHandlers.GetOrAdd(request.GetType(), static requestType =>
        {
            Type wrapperType;

            var requestInterfaceType = requestType.GetInterfaces().FirstOrDefault(static i =>
                i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IRequest<>));
            if (requestInterfaceType is null)
            {
                requestInterfaceType = requestType.GetInterfaces().FirstOrDefault(static i => i == typeof(IRequest));
                if (requestInterfaceType is null)
                    throw new ArgumentException($"{requestType.Name} does not implement {nameof(IRequest)}",
                        nameof(request));

                wrapperType = typeof(RequestHandlerWrapperImpl<>).MakeGenericType(requestType);
            }
            else
            {
                var responseType = requestInterfaceType.GetGenericArguments()[0];
                wrapperType = typeof(RequestHandlerWrapperImpl<,>).MakeGenericType(requestType, responseType);
            }

            object wrapper = Activator.CreateInstance(wrapperType) ??
                             throw new InvalidOperationException($"Could not create wrapper for type {requestType}");
            return (RequestHandlerBase)wrapper;
        });

        // call via dynamic dispatch to avoid calling through reflection for performance reasons
        return handler.Handle(request, _serviceProvider, cancellationToken);
    }

    /// <summary>
    ///     Asynchronously publishes a notification to multiple handlers.
    /// </summary>
    /// <typeparam name="TNotification">The notification type implementing <see cref="INotification" />.</typeparam>
    /// <param name="notification">The notification instance.</param>
    /// <param name="cancellationToken">Optional <see cref="CancellationToken" /> used to cancel the operation.</param>
    /// <returns>A task representing the publish operation.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="notification" /> is <c>null</c>.</exception>
    public Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default)
        where TNotification : INotification
    {
        if (notification == null) throw new ArgumentNullException(nameof(notification));

        return PublishNotification(notification, cancellationToken);
    }

    /// <summary>
    ///     Asynchronously publishes a notification object to multiple handlers via dynamic dispatch.
    /// </summary>
    /// <param name="notification">The notification instance implementing <see cref="INotification" />.</param>
    /// <param name="cancellationToken">Optional <see cref="CancellationToken" /> used to cancel the operation.</param>
    /// <returns>A task representing the publish operation.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="notification" /> is <c>null</c>.</exception>
    /// <exception cref="ArgumentException">
    ///     Thrown when <paramref name="notification" /> does not implement
    ///     <see cref="INotification" />.
    /// </exception>
    public Task Publish(object notification, CancellationToken cancellationToken = default) =>
        notification switch
        {
            null => throw new ArgumentNullException(nameof(notification)),
            INotification instance => PublishNotification(instance, cancellationToken),
            _ => throw new ArgumentException($"{nameof(notification)} does not implement ${nameof(INotification)}")
        };

    /// <summary>
    ///     Creates an async stream via a single stream handler for the given request.
    /// </summary>
    /// <typeparam name="TResponse">The response item type produced by the stream handler.</typeparam>
    /// <param name="request">The stream request implementing <see cref="IStreamRequest{TResponse}" />.</param>
    /// <param name="cancellationToken">Optional <see cref="CancellationToken" /> used to cancel the stream creation.</param>
    /// <returns>An <see cref="IAsyncEnumerable{T}" /> that yields items produced by the stream handler.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="request" /> is <c>null</c>.</exception>
    public IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamRequest<TResponse> request,
        CancellationToken cancellationToken = default)
    {
        if (request == null) throw new ArgumentNullException(nameof(request));

        var streamHandler = (StreamRequestHandlerWrapper<TResponse>)s_streamRequestHandlers.GetOrAdd(request.GetType(),
            static requestType =>
            {
                var wrapperType =
                    typeof(StreamRequestHandlerWrapperImpl<,>).MakeGenericType(requestType, typeof(TResponse));
                object wrapper = Activator.CreateInstance(wrapperType) ??
                                 throw new InvalidOperationException(
                                     $"Could not create wrapper for type {requestType}");
                return (StreamRequestHandlerBase)wrapper;
            });

        var items = streamHandler.Handle(request, _serviceProvider, cancellationToken);

        return items;
    }

    /// <summary>
    ///     Creates an async stream via dynamic dispatch to the appropriate stream handler.
    /// </summary>
    /// <param name="request">The stream request object implementing <see cref="IStreamRequest{TResponse}" />.</param>
    /// <param name="cancellationToken">Optional <see cref="CancellationToken" /> used to cancel the stream creation.</param>
    /// <returns>An <see cref="IAsyncEnumerable{Object}" /> that yields items produced by the stream handler.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="request" /> is <c>null</c>.</exception>
    /// <exception cref="ArgumentException">
    ///     Thrown when <paramref name="request" /> does not implement
    ///     <c>IStreamRequest&lt;TResponse&gt;</c>.
    /// </exception>
    public IAsyncEnumerable<object?> CreateStream(object request, CancellationToken cancellationToken = default)
    {
        if (request == null) throw new ArgumentNullException(nameof(request));

        var handler = s_streamRequestHandlers.GetOrAdd(request.GetType(), static requestType =>
        {
            var requestInterfaceType = requestType.GetInterfaces().FirstOrDefault(static i =>
                i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IStreamRequest<>));
            if (requestInterfaceType is null)
                throw new ArgumentException($"{requestType.Name} does not implement IStreamRequest<TResponse>",
                    nameof(request));

            var responseType = requestInterfaceType.GetGenericArguments()[0];
            var wrapperType = typeof(StreamRequestHandlerWrapperImpl<,>).MakeGenericType(requestType, responseType);
            object wrapper = Activator.CreateInstance(wrapperType) ??
                             throw new InvalidOperationException($"Could not create wrapper for type {requestType}");
            return (StreamRequestHandlerBase)wrapper;
        });

        var items = handler.Handle(request, _serviceProvider, cancellationToken);

        return items;
    }

    /// <summary>
    ///     Override in a derived class to control how notification handler tasks are awaited.
    ///     By default, delegates to the configured <see cref="INotificationPublisher" />.
    /// </summary>
    /// <param name="handlerExecutors">Enumerable of handler executors representing invocation of each notification handler.</param>
    /// <param name="notification">The notification being published.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the invocation of all handlers.</returns>
    protected virtual Task PublishCore(IEnumerable<NotificationHandlerExecutor> handlerExecutors,
        INotification notification, CancellationToken cancellationToken)
        => _publisher.Publish(handlerExecutors, notification, cancellationToken);

    /// <summary>
    ///     Publishes a strongly-typed notification using the cached wrapper and the configured publisher.
    /// </summary>
    /// <param name="notification">The notification instance.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the publish operation.</returns>
    private Task PublishNotification(INotification notification, CancellationToken cancellationToken = default)
    {
        var handler = s_notificationHandlers.GetOrAdd(notification.GetType(), static notificationType =>
        {
            var wrapperType = typeof(NotificationHandlerWrapperImpl<>).MakeGenericType(notificationType);
            object wrapper = Activator.CreateInstance(wrapperType) ??
                             throw new InvalidOperationException(
                                 $"Could not create wrapper for type {notificationType}");
            return (NotificationHandlerWrapper)wrapper;
        });

        return handler.Handle(notification, _serviceProvider, PublishCore, cancellationToken);
    }
}