namespace Umlamuli;

/// <summary>
///     Defines a strategy for publishing notifications to registered handlers.
/// </summary>
/// <remarks>
///     Implementations control how notification handlers are invoked (e.g., sequentially or in parallel),
///     and how exceptions are handled. The publisher should honor the provided <see cref="CancellationToken" />.
/// </remarks>
public interface INotificationPublisher
{
    /// <summary>
    ///     Publishes a notification to the specified handler executors.
    /// </summary>
    /// <param name="handlerExecutors">
    ///     A sequence of <see cref="NotificationHandlerExecutor" /> instances that encapsulate the logic to invoke
    ///     notification handlers. Implementations may choose the invocation strategy (e.g., sequential or concurrent).
    /// </param>
    /// <param name="notification">
    ///     The notification instance to be dispatched to handlers implementing <see cref="INotification" />.
    /// </param>
    /// <param name="cancellationToken">
    ///     A token to observe while awaiting publication operations. If cancellation is requested,
    ///     implementations should stop further processing and propagate cancellation where appropriate.
    /// </param>
    /// <returns>
    ///     A task representing the asynchronous publish operation.
    /// </returns>
    /// <remarks>
    ///     Implementations should ensure robust error handling; depending on policy, exceptions from handlers
    ///     may be aggregated, swallowed, or rethrown. Ordering guarantees, if any, should be documented by the implementation.
    /// </remarks>
    Task Publish(IEnumerable<NotificationHandlerExecutor> handlerExecutors, INotification notification,
        CancellationToken cancellationToken);
}