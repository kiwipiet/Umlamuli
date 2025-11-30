namespace Umlamuli.Pipeline;

/// <summary>
///     Behavior for executing all <see cref="IRequestPostProcessor{TRequest,TResponse}" /> instances after handling the
///     request.
/// </summary>
/// <typeparam name="TRequest">Request type.</typeparam>
/// <typeparam name="TResponse">Response type.</typeparam>
public class RequestPostProcessorBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    /// <summary>
    ///     The collection of post-processors to execute after the request handler completes.
    /// </summary>
    private readonly IEnumerable<IRequestPostProcessor<TRequest, TResponse>> _postProcessors;

    /// <summary>
    ///     Initializes a new instance of the <see cref="RequestPostProcessorBehavior{TRequest, TResponse}" /> class.
    /// </summary>
    /// <param name="postProcessors">
    ///     The collection of <see cref="IRequestPostProcessor{TRequest, TResponse}" /> instances to
    ///     be invoked.
    /// </param>
    public RequestPostProcessorBehavior(IEnumerable<IRequestPostProcessor<TRequest, TResponse>> postProcessors)
        => _postProcessors = postProcessors;

    /// <summary>
    ///     Executes the inner handler to obtain the response, then runs all configured
    ///     <see cref="IRequestPostProcessor{TRequest, TResponse}" /> instances.
    /// </summary>
    /// <param name="request">The incoming request.</param>
    /// <param name="next">The delegate representing the next action in the pipeline (typically the handler).</param>
    /// <param name="cancellationToken">A token to observe while waiting for completion.</param>
    /// <returns>A task that represents the asynchronous operation, containing the <typeparamref name="TResponse" />.</returns>
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        if (next == null) throw new ArgumentNullException(nameof(next));

        var response = await next(cancellationToken).ConfigureAwait(false);

        foreach (var processor in _postProcessors)
            await processor.Process(request, response, cancellationToken).ConfigureAwait(false);

        return response;
    }
}