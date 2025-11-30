using Microsoft.Extensions.DependencyInjection;

namespace Umlamuli.Wrappers;

/// <summary>
///     Base wrapper abstraction for handling requests dynamically.
/// </summary>
/// <remarks>
///     This type unifies handling for both requests returning a response and requests returning <see cref="Unit" />.
///     Concrete implementations adapt strongly-typed handlers to an <see cref="object" />-based interface.
/// </remarks>
internal abstract class RequestHandlerBase
{
    /// <summary>
    ///     Handles the provided <paramref name="request" /> using services from the given <paramref name="serviceProvider" />.
    /// </summary>
    /// <param name="request">
    ///     The request instance to handle. Expected to implement either <see cref="IRequest{TResponse}" />
    ///     or <see cref="IRequest" />.
    /// </param>
    /// <param name="serviceProvider">The service provider used to resolve handlers and pipeline behaviors.</param>
    /// <param name="cancellationToken">A token that can be used to cancel the operation.</param>
    /// <returns>
    ///     A task that completes with either a response object (for <see cref="IRequest{TResponse}" />)
    ///     or <c>null</c> (for <see cref="IRequest" /> that returns <see cref="Unit" />).
    /// </returns>
    public abstract Task<object?> Handle(object request, IServiceProvider serviceProvider,
        CancellationToken cancellationToken);
}

/// <summary>
///     Wrapper abstraction for handling requests that produce a response.
/// </summary>
/// <typeparam name="TResponse">The type of response produced by the request.</typeparam>
/// <remarks>
///     Implementations resolve <see cref="IRequestHandler{TRequest, TResponse}" /> and apply registered
///     <see cref="IPipelineBehavior{TRequest, TResponse}" /> instances.
/// </remarks>
internal abstract class RequestHandlerWrapper<TResponse> : RequestHandlerBase
{
    /// <summary>
    ///     Handles the provided <paramref name="request" /> and produces a <typeparamref name="TResponse" />.
    /// </summary>
    /// <param name="request">The strongly-typed request instance.</param>
    /// <param name="serviceProvider">The service provider used to resolve the request handler and pipeline behaviors.</param>
    /// <param name="cancellationToken">A token that can be used to cancel the operation.</param>
    /// <returns>A task representing the asynchronous operation which yields a <typeparamref name="TResponse" />.</returns>
    public abstract Task<TResponse> Handle(IRequest<TResponse> request, IServiceProvider serviceProvider,
        CancellationToken cancellationToken);
}

/// <summary>
///     Wrapper abstraction for handling requests that do not produce a response (return <see cref="Unit" />).
/// </summary>
/// <remarks>
///     Implementations resolve <see cref="IRequestHandler{TRequest}" /> and apply registered
///     <see cref="IPipelineBehavior{TRequest, TResponse}" /> instances with <see cref="Unit" /> as the response.
/// </remarks>
internal abstract class RequestHandlerWrapper : RequestHandlerBase
{
    /// <summary>
    ///     Handles the provided <paramref name="request" /> and returns <see cref="Unit" />.
    /// </summary>
    /// <param name="request">The strongly-typed request instance.</param>
    /// <param name="serviceProvider">The service provider used to resolve the request handler and pipeline behaviors.</param>
    /// <param name="cancellationToken">A token that can be used to cancel the operation.</param>
    /// <returns>A task representing the asynchronous operation which yields <see cref="Unit" />.</returns>
    public abstract Task<Unit> Handle(IRequest request, IServiceProvider serviceProvider,
        CancellationToken cancellationToken);
}

/// <summary>
///     Internal wrapper implementation for request/response handling with pipelines.
/// </summary>
/// <typeparam name="TRequest">The request type implementing <see cref="IRequest{TResponse}" />.</typeparam>
/// <typeparam name="TResponse">The response type produced by the request.</typeparam>
/// <remarks>
///     Resolves the concrete <see cref="IRequestHandler{TRequest, TResponse}" /> and composes registered
///     <see cref="IPipelineBehavior{TRequest, TResponse}" />
///     in reverse registration order to build an invocation chain.
/// </remarks>
internal class RequestHandlerWrapperImpl<TRequest, TResponse> : RequestHandlerWrapper<TResponse>
    where TRequest : IRequest<TResponse>
{
    /// <inheritdoc />
    public override async Task<object?> Handle(object request, IServiceProvider serviceProvider,
        CancellationToken cancellationToken) =>
        await Handle((IRequest<TResponse>)request, serviceProvider, cancellationToken).ConfigureAwait(false);

    /// <summary>
    ///     Handles the provided typed <paramref name="request" /> by invoking the resolved handler through the composed
    ///     pipeline behaviors.
    /// </summary>
    /// <param name="request">The request instance implementing <see cref="IRequest{TResponse}" />.</param>
    /// <param name="serviceProvider">The service provider used to resolve the handler and behaviors.</param>
    /// <param name="cancellationToken">A token that can be used to cancel the operation.</param>
    /// <returns>A task yielding the <typeparamref name="TResponse" /> produced by the handler.</returns>
    /// <remarks>
    ///     - The inner handler and each pipeline behavior receive a cancellation token. If a behavior supplies
    ///     <see cref="CancellationToken.None" />, the original <paramref name="cancellationToken" /> is used to ensure
    ///     propagation.
    ///     - Pipelines are applied in reverse order so the last registered behavior executes first.
    /// </remarks>
    public override Task<TResponse> Handle(IRequest<TResponse> request, IServiceProvider serviceProvider,
        CancellationToken cancellationToken)
    {
        Task<TResponse> Handler(CancellationToken t = default)
        {
            return serviceProvider.GetRequiredService<IRequestHandler<TRequest, TResponse>>()
                .Handle((TRequest)request, t == CancellationToken.None ? cancellationToken : t);
        }

        return serviceProvider
            .GetServices<IPipelineBehavior<TRequest, TResponse>>()
            .Reverse()
            .Aggregate((RequestHandlerDelegate<TResponse>)Handler,
                (next, pipeline) =>
                    t => pipeline.Handle((TRequest)request, next,
                        t == CancellationToken.None ? cancellationToken : t))();
    }
}

/// <summary>
///     Internal wrapper implementation for request handling that returns <see cref="Unit" />.
/// </summary>
/// <typeparam name="TRequest">The request type implementing <see cref="IRequest" />.</typeparam>
/// <remarks>
///     Resolves the concrete <see cref="IRequestHandler{TRequest}" /> and composes registered
///     <see cref="IPipelineBehavior{TRequest, Unit}" />
///     in reverse registration order to build an invocation chain.
/// </remarks>
internal class RequestHandlerWrapperImpl<TRequest> : RequestHandlerWrapper
    where TRequest : IRequest
{
    /// <inheritdoc />
    public override async Task<object?> Handle(object request, IServiceProvider serviceProvider,
        CancellationToken cancellationToken) =>
        await Handle((IRequest)request, serviceProvider, cancellationToken).ConfigureAwait(false);

    /// <summary>
    ///     Handles the provided typed <paramref name="request" /> by invoking the resolved handler through the composed
    ///     pipeline behaviors.
    /// </summary>
    /// <param name="request">The request instance implementing <see cref="IRequest" />.</param>
    /// <param name="serviceProvider">The service provider used to resolve the handler and behaviors.</param>
    /// <param name="cancellationToken">A token that can be used to cancel the operation.</param>
    /// <returns>A task yielding <see cref="Unit" />.</returns>
    /// <remarks>
    ///     - The inner handler and each pipeline behavior receive a cancellation token. If a behavior supplies
    ///     <see cref="CancellationToken.None" />, the original <paramref name="cancellationToken" /> is used to ensure
    ///     propagation.
    ///     - Pipelines are applied in reverse order so the last registered behavior executes first.
    /// </remarks>
    public override Task<Unit> Handle(IRequest request, IServiceProvider serviceProvider,
        CancellationToken cancellationToken)
    {
        async Task<Unit> Handler(CancellationToken t = default)
        {
            await serviceProvider.GetRequiredService<IRequestHandler<TRequest>>()
                .Handle((TRequest)request, t == CancellationToken.None ? cancellationToken : t);

            return Unit.Value;
        }

        return serviceProvider
            .GetServices<IPipelineBehavior<TRequest, Unit>>()
            .Reverse()
            .Aggregate((RequestHandlerDelegate<Unit>)Handler,
                (next, pipeline) =>
                    t => pipeline.Handle((TRequest)request, next,
                        t == CancellationToken.None ? cancellationToken : t))();
    }
}