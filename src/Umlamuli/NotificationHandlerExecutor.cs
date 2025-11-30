namespace Umlamuli;

/// <summary>
///     Encapsulates a notification handler instance alongside its execution callback.
/// </summary>
/// <remarks>
///     This record binds a created handler to a delegate capable of invoking the handler logic for a given
///     <see cref="INotification" /> and <see cref="CancellationToken" />. It allows decoupled execution where
///     consumers can store and trigger handler execution without needing to know the concrete handler type.
///     The delegate should be thread-safe if handlers are executed concurrently.
/// </remarks>
/// <param name="HandlerInstance">
///     The constructed notification handler instance associated with the execution callback. This is typically
///     an implementation of a notification handler interface for a specific <see cref="INotification" /> type.
/// </param>
/// <param name="HandlerCallback">
///     A delegate that executes the handler logic given an <see cref="INotification" /> and a
///     <see cref="CancellationToken" />. It should return a task representing the asynchronous execution
///     of the handler.
/// </param>
/// <seealso cref="INotification" />
public record NotificationHandlerExecutor(
    object HandlerInstance,
    Func<INotification, CancellationToken, Task> HandlerCallback
);