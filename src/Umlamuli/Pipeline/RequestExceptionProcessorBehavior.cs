using System.Reflection;
using System.Runtime.ExceptionServices;
using Microsoft.Extensions.DependencyInjection;
using Umlamuli.Internal;

namespace Umlamuli.Pipeline;

/// <summary>
///     Pipeline behavior that intercepts exceptions thrown by subsequent handlers and invokes all registered
///     <see cref="IRequestExceptionHandler{TRequest,TResponse,TException}" /> implementations for the specific exception
///     type.
///     The first handler that marks the exception as handled determines the final <typeparamref name="TResponse" />
///     returned.
/// </summary>
/// <typeparam name="TRequest">The request type flowing through the pipeline.</typeparam>
/// <typeparam name="TResponse">The response type produced by the pipeline.</typeparam>
public class RequestExceptionProcessorBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    /// <summary>
    ///     The <see cref="IServiceProvider" /> used to resolve collections of
    ///     <see cref="IRequestExceptionHandler{TRequest,TResponse,TException}" /> instances.
    /// </summary>
    private readonly IServiceProvider _serviceProvider;

    /// <summary>
    ///     Initializes a new instance of the <see cref="RequestExceptionProcessorBehavior{TRequest, TResponse}" /> class.
    /// </summary>
    /// <param name="serviceProvider">The service provider used to resolve exception handlers.</param>
    public RequestExceptionProcessorBehavior(IServiceProvider serviceProvider) => _serviceProvider = serviceProvider;

    /// <summary>
    ///     Executes the next pipeline delegate and, if an exception is thrown, iterates through all compatible
    ///     exception handlers for the specific exception hierarchy. Handlers are de-duplicated by concrete type,
    ///     prioritized via <see cref="HandlersOrderer.Prioritize{T}(IList{object},T)" />, and
    ///     invoked until one
    ///     marks the exception as handled. If handled, the response from
    ///     <see cref="RequestExceptionHandlerState{TResponse}.Response" />
    ///     is returned; otherwise the original exception is rethrown.
    /// </summary>
    /// <param name="request">The current request instance.</param>
    /// <param name="next">The next delegate in the pipeline.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>
    ///     A task that completes with the response of either the next delegate or the exception handler that handled the
    ///     exception.
    /// </returns>
    /// <exception cref="InvalidOperationException">
    ///     Thrown if an exception handler does not return a <see cref="Task" /> from
    ///     its <c>Handle</c> method or the handler method cannot be located.
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
            var state = new RequestExceptionHandlerState<TResponse>();

            var exceptionTypes = GetExceptionTypes(exception.GetType());

            var handlersForException = exceptionTypes
                .SelectMany(exceptionType => GetHandlersForException(exceptionType, request))
                .GroupBy(static handlerForException => handlerForException.Handler.GetType())
                .Select(static handlerForException => handlerForException.First())
                .Select(static handlerForException => (
                    MethodInfo: GetMethodInfoForHandler(handlerForException.ExceptionType),
                    handlerForException.Handler))
                .ToList();

            foreach (var handlerForException in handlersForException)
            {
                try
                {
                    // Invoke IRequestExceptionHandler<TRequest,TResponse,TException>.Handle
                    await ((Task)(handlerForException.MethodInfo.Invoke(handlerForException.Handler,
                                      [request, exception, state, cancellationToken])
                                  ?? throw new InvalidOperationException(
                                      "Did not return a Task from the exception handler."))).ConfigureAwait(false);
                }
                catch (TargetInvocationException invocationException) when (invocationException.InnerException != null)
                {
                    // Unwrap invocation exception to throw the actual error
                    ExceptionDispatchInfo.Capture(invocationException.InnerException).Throw();
                }

                if (state.Handled) break;
            }

            if (!state.Handled) throw;

            if (state.Response is null) throw;

            // Cannot be null if Handled
            return state.Response;
        }
    }

    /// <summary>
    ///     Enumerates the given exception type and all its base types up to but excluding <see cref="object" />.
    /// </summary>
    /// <param name="exceptionType">The concrete exception type to expand.</param>
    /// <returns>An enumeration of the exception type followed by its base types.</returns>
    private static IEnumerable<Type> GetExceptionTypes(Type? exceptionType)
    {
        while (exceptionType != null && exceptionType != typeof(object))
        {
            yield return exceptionType;
            exceptionType = exceptionType.BaseType;
        }
    }

    /// <summary>
    ///     Resolves all registered exception handlers compatible with the specified exception type and current request,
    ///     then applies prioritization based on <see cref="HandlersOrderer" />.
    /// </summary>
    /// <param name="exceptionType">The specific exception type to match handler generic arguments.</param>
    /// <param name="request">The current request, used for prioritization ordering.</param>
    /// <returns>A sequence of tuples containing the exception type and the resolved handler instance.</returns>
    /// <remarks>
    ///     Handlers are resolved via
    ///     <see
    ///         cref="Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService(IServiceProvider, Type)" />
    ///     for
    ///     <see cref="IEnumerable{T}" /> of <see cref="IRequestExceptionHandler{TRequest,TResponse,TException}" />.
    /// </remarks>
    private IEnumerable<(Type ExceptionType, object Handler)> GetHandlersForException(Type exceptionType,
        TRequest request)
    {
        var exceptionHandlerInterfaceType =
            typeof(IRequestExceptionHandler<,,>).MakeGenericType(typeof(TRequest), typeof(TResponse), exceptionType);
        var enumerableExceptionHandlerInterfaceType =
            typeof(IEnumerable<>).MakeGenericType(exceptionHandlerInterfaceType);

        var exceptionHandlers =
            (IEnumerable<object>)_serviceProvider.GetRequiredService(enumerableExceptionHandlerInterfaceType);

        return HandlersOrderer.Prioritize(exceptionHandlers.ToList(), request)
            .Select(handler => (exceptionType, action: handler));
    }

    /// <summary>
    ///     Retrieves the <c>Handle</c> method metadata from the constructed
    ///     <see cref="IRequestExceptionHandler{TRequest,TResponse,TException}" /> interface for the given exception type.
    /// </summary>
    /// <param name="exceptionType">The exception type used to construct the generic handler interface.</param>
    /// <returns>The <see cref="MethodInfo" /> for the handler's <c>Handle</c> method.</returns>
    /// <exception cref="InvalidOperationException">
    ///     Thrown if the <c>Handle</c> method cannot be found on the constructed
    ///     interface.
    /// </exception>
    private static MethodInfo GetMethodInfoForHandler(Type exceptionType)
    {
        var exceptionHandlerInterfaceType =
            typeof(IRequestExceptionHandler<,,>).MakeGenericType(typeof(TRequest), typeof(TResponse), exceptionType);

        var handleMethodInfo =
            exceptionHandlerInterfaceType.GetMethod(nameof(IRequestExceptionHandler<TRequest, TResponse, Exception>
                .Handle))
            ?? throw new InvalidOperationException(
                $"Could not find method {nameof(IRequestExceptionHandler<TRequest, TResponse, Exception>.Handle)} on type {exceptionHandlerInterfaceType}");

        return handleMethodInfo;
    }
}