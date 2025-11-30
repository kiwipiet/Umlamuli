using Microsoft.Extensions.DependencyInjection;

namespace Umlamuli.Wrappers;

/// <summary>
///     Provides an abstraction for invoking notification handlers in a generic manner.
/// </summary>
/// <remarks>
///     This wrapper allows notifications to be handled without knowing their concrete types at compile time.
///     Implementations resolve the appropriate <see cref="INotificationHandler{TNotification}" /> instances
///     and pass them to a publish function that controls how handlers are executed (e.g., sequentially or in parallel).
/// </remarks>
internal abstract class NotificationHandlerWrapper
{
    /// <summary>
    ///     Handles the specified <paramref name="notification" /> by resolving its handlers and invoking the provided
    ///     <paramref name="publish" /> function.
    /// </summary>
    /// <param name="notification">The notification instance to process.</param>
    /// <param name="serviceFactory">The service provider used to resolve handler instances.</param>
    /// <param name="publish">
    ///     A delegate that receives the resolved <see cref="NotificationHandlerExecutor" /> sequence, the original
    ///     notification,
    ///     and a <see cref="CancellationToken" />, and returns a <see cref="Task" /> representing the publish operation.
    /// </param>
    /// <param name="cancellationToken">A token to observe while waiting for the task to complete.</param>
    /// <returns>A task representing the asynchronous handling operation.</returns>
    public abstract Task Handle(INotification notification, IServiceProvider serviceFactory,
        Func<IEnumerable<NotificationHandlerExecutor>, INotification, CancellationToken, Task> publish,
        CancellationToken cancellationToken);
}

/// <summary>
///     Concrete wrapper that resolves and executes <see cref="INotificationHandler{TNotification}" /> implementations for
///     a specific notification type.
/// </summary>
/// <typeparam name="TNotification">The specific notification type to handle.</typeparam>
/// <remarks>
///     Handlers are resolved via <see cref="IServiceProvider" /> using
///     <see cref="ServiceProviderServiceExtensions.GetServices{T}(IServiceProvider)" />.
///     Each handler is wrapped in a <see cref="NotificationHandlerExecutor" /> to provide a uniform execution surface.
/// </remarks>
internal class NotificationHandlerWrapperImpl<TNotification> : NotificationHandlerWrapper
    where TNotification : INotification
{
    /// <summary>
    ///     Resolves all <see cref="INotificationHandler{TNotification}" /> instances and publishes them using the supplied
    ///     <paramref name="publish" /> delegate.
    /// </summary>
    /// <param name="notification">
    ///     The notification instance to process. Must be compatible with
    ///     <typeparamref name="TNotification" />.
    /// </param>
    /// <param name="serviceFactory">
    ///     The service provider used to resolve <see cref="INotificationHandler{TNotification}" />
    ///     implementations.
    /// </param>
    /// <param name="publish">
    ///     The function that orchestrates execution of the resolved handlers. It receives the handlers wrapped as
    ///     <see cref="NotificationHandlerExecutor" />, the original notification, and the cancellation token.
    /// </param>
    /// <param name="cancellationToken">A token to cancel the publish operation.</param>
    /// <returns>A task that completes when the publish operation finishes.</returns>
    /// <remarks>
    ///     Handlers are projected into <see cref="NotificationHandlerExecutor" /> with a strongly-typed callback that casts
    ///     <paramref name="notification" /> to <typeparamref name="TNotification" /> before invoking
    ///     <see cref="INotificationHandler{TNotification}.Handle(TNotification, CancellationToken)" />.
    /// </remarks>
    public override Task Handle(INotification notification, IServiceProvider serviceFactory,
        Func<IEnumerable<NotificationHandlerExecutor>, INotification, CancellationToken, Task> publish,
        CancellationToken cancellationToken)
    {
        var handlers = serviceFactory
            .GetServices<INotificationHandler<TNotification>>()
            .Select(static x => new NotificationHandlerExecutor(x,
                (theNotification, theToken) => x.Handle((TNotification)theNotification, theToken)));

        return publish(handlers, notification, cancellationToken);
    }
}