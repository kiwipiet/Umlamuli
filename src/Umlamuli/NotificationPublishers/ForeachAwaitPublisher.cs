namespace Umlamuli.NotificationPublishers;

/// <summary>
///     An <see cref="INotificationPublisher" /> implementation that invokes each notification handler
///     sequentially in a single <c>foreach</c> loop, awaiting completion before moving to the next.
/// </summary>
/// <code>
/// foreach (var handler in handlers) {
///     await handler(notification, cancellationToken);
/// }
/// </code>
public class ForeachAwaitPublisher : INotificationPublisher
{
    /// <summary>
    ///     Publishes the specified <paramref name="notification" /> to each handler executor sequentially,
    ///     awaiting completion of one before invoking the next.
    /// </summary>
    /// <param name="handlerExecutors">
    ///     The sequence of <see cref="NotificationHandlerExecutor" /> instances representing handler callbacks.
    ///     Invocation order is preserved as provided by the enumerable.
    /// </param>
    /// <param name="notification">
    ///     The notification instance to dispatch.
    /// </param>
    /// <param name="cancellationToken">
    ///     A token to observe for cancellation. If cancellation is requested before or during execution of a handler,
    ///     the operation stops and a <see cref="TaskCanceledException" /> (or related cancellation) may be thrown.
    /// </param>
    /// <returns>
    ///     A task representing the asynchronous publish operation.
    /// </returns>
    /// <remarks>
    ///     Ordering is preserved; handlers are invoked in the order provided by <paramref name="handlerExecutors" />.
    ///     If a handler throws, the exception is propagated immediately and subsequent handlers are not invoked.
    ///     Cancellation is observed prior to each handler invocation through the provided
    ///     <see cref="CancellationToken" />.
    /// </remarks>
    /// <exception cref="System.Exception">
    ///     Propagates any exception thrown by a handler callback; remaining handlers are not executed.
    /// </exception>
    public async Task Publish(IEnumerable<NotificationHandlerExecutor> handlerExecutors, INotification notification,
        CancellationToken cancellationToken)
    {
        // ReSharper disable once ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
        if (handlerExecutors == null)
            return;

        foreach (var handler in handlerExecutors)
        {
            // Cancellation requested: honor promptly
            cancellationToken.ThrowIfCancellationRequested();
            await handler.HandlerCallback(notification, cancellationToken).ConfigureAwait(false);
        }
    }
}