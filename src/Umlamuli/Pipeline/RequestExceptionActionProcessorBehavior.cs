using System.Reflection;
using System.Runtime.ExceptionServices;
using Microsoft.Extensions.DependencyInjection;
using Umlamuli.Internal;

namespace Umlamuli.Pipeline;

/// <summary>
///     Behavior for executing all <see cref="IRequestExceptionAction{TRequest,TException}" /> instances
///     after an exception is thrown by the following pipeline steps.
/// </summary>
/// <typeparam name="TRequest">Request type.</typeparam>
/// <typeparam name="TResponse">Response type.</typeparam>
public class RequestExceptionActionProcessorBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    /// <summary>
    ///     The service provider used to resolve registered <see cref="IRequestExceptionAction{TRequest,TException}" />
    ///     implementations.
    /// </summary>
    private readonly IServiceProvider _serviceProvider;

    /// <summary>
    ///     Initializes a new instance of the <see cref="RequestExceptionActionProcessorBehavior{TRequest, TResponse}" />
    ///     class.
    /// </summary>
    /// <param name="serviceProvider">
    ///     The <see cref="IServiceProvider" /> used to resolve collections of
    ///     <see cref="IRequestExceptionAction{TRequest,TException}" /> for handled exception types.
    /// </param>
    public RequestExceptionActionProcessorBehavior(IServiceProvider serviceProvider) =>
        _serviceProvider = serviceProvider;

    /// <summary>
    ///     Executes the next pipeline delegate and, if an exception occurs, resolves and runs all matching
    ///     <see cref="IRequestExceptionAction{TRequest,TException}" /> implementations for the thrown exception and its base
    ///     types.
    /// </summary>
    /// <param name="request">The incoming request instance.</param>
    /// <param name="next">The next delegate in the pipeline to invoke.</param>
    /// <param name="cancellationToken">A token to observe while waiting for the task to complete.</param>
    /// <returns>The response from the inner pipeline/handler.</returns>
    /// <exception cref="InvalidOperationException">
    ///     Thrown when an action's task cannot be created or when required services for actions are not registered.
    /// </exception>
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        if (next == null) throw new ArgumentNullException(nameof(next));

        try
        {
            return await next(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            var exceptionTypes = GetExceptionTypes(exception.GetType());

            var actionsForException = exceptionTypes
                .SelectMany(exceptionType => GetActionsForException(exceptionType, request))
                .GroupBy(static actionForException => actionForException.Action.GetType())
                .Select(static actionForException => actionForException.First())
                .Select(static actionForException => (
                    MethodInfo: GetMethodInfoForAction(actionForException.ExceptionType), actionForException.Action))
                .ToList();

            foreach (var actionForException in actionsForException)
                try
                {
                    await ((Task)(actionForException.MethodInfo.Invoke(actionForException.Action,
                                      [request, exception, cancellationToken])
                                  ?? throw new InvalidOperationException(
                                      $"Could not create task for action method {actionForException.MethodInfo}.")))
                        .ConfigureAwait(false);
                }
                catch (TargetInvocationException invocationException) when (invocationException.InnerException != null)
                {
                    // Unwrap invocation exception to throw the actual error
                    ExceptionDispatchInfo.Capture(invocationException.InnerException).Throw();
                }

            throw;
        }
    }

    /// <summary>
    ///     Enumerates the specified exception type and all of its base types up to (but excluding) <see cref="object" />.
    /// </summary>
    /// <param name="exceptionType">The starting exception type.</param>
    /// <returns>A sequence of the exception type and its base types.</returns>
    private static IEnumerable<Type> GetExceptionTypes(Type? exceptionType)
    {
        while (exceptionType != null && exceptionType != typeof(object))
        {
            yield return exceptionType;
            exceptionType = exceptionType.BaseType;
        }
    }

    /// <summary>
    ///     Resolves and prioritizes all registered <see cref="IRequestExceptionAction{TRequest,TException}" /> implementations
    ///     for a specific exception type.
    /// </summary>
    /// <param name="exceptionType">Concrete or base exception type being processed.</param>
    /// <param name="request">The request instance used for prioritization logic.</param>
    /// <returns>
    ///     An ordered sequence of tuples containing the exception type and the corresponding action instance.
    /// </returns>
    /// <remarks>
    ///     Uses the service provider to resolve <see cref="IEnumerable{T}" /> of the generic action interface,
    ///     then applies <see cref="HandlersOrderer.Prioritize" /> to determine invocation order.
    /// </remarks>
    /// <exception cref="InvalidOperationException">
    ///     Thrown if the required enumerable service has not been registered.
    /// </exception>
    private IEnumerable<(Type ExceptionType, object Action)> GetActionsForException(Type exceptionType,
        TRequest request)
    {
        var exceptionActionInterfaceType =
            typeof(IRequestExceptionAction<,>).MakeGenericType(typeof(TRequest), exceptionType);
        var enumerableExceptionActionInterfaceType =
            typeof(IEnumerable<>).MakeGenericType(exceptionActionInterfaceType);

        var actionsForException =
            (IEnumerable<object>)_serviceProvider.GetRequiredService(enumerableExceptionActionInterfaceType);

        return HandlersOrderer.Prioritize(actionsForException.ToList(), request)
            .Select(action => (exceptionType, action));
    }

    /// <summary>
    ///     Retrieves the <see cref="MethodInfo" /> for the <c>Execute</c> method on the constructed
    ///     <see cref="IRequestExceptionAction{TRequest,TException}" /> interface for the provided exception type.
    /// </summary>
    /// <param name="exceptionType">The exception type used to construct the generic action interface.</param>
    /// <returns>The <see cref="MethodInfo" /> representing the Execute method.</returns>
    /// <exception cref="InvalidOperationException">
    ///     Thrown when the Execute method cannot be located on the constructed interface.
    /// </exception>
    private static MethodInfo GetMethodInfoForAction(Type exceptionType)
    {
        var exceptionActionInterfaceType =
            typeof(IRequestExceptionAction<,>).MakeGenericType(typeof(TRequest), exceptionType);

        var actionMethodInfo =
            exceptionActionInterfaceType.GetMethod(nameof(IRequestExceptionAction<TRequest, Exception>.Execute))
            ?? throw new InvalidOperationException(
                $"Could not find method {nameof(IRequestExceptionAction<TRequest, Exception>.Execute)} on type {exceptionActionInterfaceType}");

        return actionMethodInfo;
    }
}