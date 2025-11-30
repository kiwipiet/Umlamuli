namespace Umlamuli.NotificationPublishers;

/// <summary>
///     Uses <see cref="Task.WhenAll(System.Collections.Generic.IEnumerable{System.Threading.Tasks.Task})" /> to publish a
///     notification
///     to all provided handlers concurrently.
/// </summary>
/// <code>
/// var tasks = handlers
///                .Select(handler => handler.Handle(notification, cancellationToken))
///                .ToList();
/// 
/// return Task.WhenAll(tasks);
/// </code>
/// <remarks>
///     The publisher enumerates all <see cref="NotificationHandlerExecutor" /> instances, invokes each handler callback
///     immediately,
///     captures the resulting tasks, and awaits their completion in aggregate via
///     <see cref="Task.WhenAll(System.Threading.Tasks.Task[])" />.
///     All handlers are started eagerly; no attempt is made to short‑circuit on first failure or cancellation.
///     Exceptions thrown by individual handlers are aggregated. Ordering of handler execution or completion is not
///     guaranteed.
/// </remarks>
public class TaskWhenAllPublisher : INotificationPublisher
{
    /// <summary>
    ///     Publishes the specified <paramref name="notification" /> to all <paramref name="handlerExecutors" /> concurrently.
    /// </summary>
    /// <param name="handlerExecutors">
    ///     A sequence of <see cref="NotificationHandlerExecutor" /> instances whose <c>HandlerCallback</c> delegates
    ///     are invoked to process the notification. The sequence is materialized to avoid multiple enumeration.
    /// </param>
    /// <param name="notification">
    ///     The notification instance to dispatch to registered handlers.
    /// </param>
    /// <param name="cancellationToken">
    ///     A token observed by each handler during its execution. If cancellation is requested, handlers that honor the token
    ///     may terminate early. Cancellation does not stop already-started handlers from running; all initiated tasks are
    ///     awaited.
    /// </param>
    /// <returns>
    ///     A task that completes when all handler tasks have finished. If one or more handlers fault, the returned task faults
    ///     with an <see cref="AggregateException" /> containing all encountered exceptions. If cancellation is requested
    ///     before any
    ///     handler faults and handlers cooperatively cancel, the returned task may transition to a canceled state.
    /// </returns>
    /// <remarks>
    ///     Execution is fully concurrent relative to handler invocation; ordering guarantees are not provided.
    ///     No exception handling or filtering is performed beyond aggregation by
    ///     <see cref="Task.WhenAll(System.Threading.Tasks.Task[])" />.
    /// </remarks>
    public Task Publish(IEnumerable<NotificationHandlerExecutor> handlerExecutors, INotification notification,
        CancellationToken cancellationToken)
    {
        var tasks = handlerExecutors
            .Select(handler => handler.HandlerCallback(notification, cancellationToken))
            .ToArray();

        return Task.WhenAll(tasks);
    }
}