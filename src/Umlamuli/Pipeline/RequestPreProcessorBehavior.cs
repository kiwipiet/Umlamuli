namespace Umlamuli.Pipeline;

/// <summary>
///     Behavior for executing all <see cref="IRequestPreProcessor{TRequest}" /> instances before handling a request.
/// </summary>
/// <typeparam name="TRequest">The request type.</typeparam>
/// <typeparam name="TResponse">The response type.</typeparam>
public class RequestPreProcessorBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    /// <summary>
    ///     The collection of request pre-processors to execute prior to the request handler.
    /// </summary>
    private readonly IEnumerable<IRequestPreProcessor<TRequest>> _preProcessors;

    /// <summary>
    ///     Initializes a new instance of the <see cref="RequestPreProcessorBehavior{TRequest, TResponse}" /> class.
    /// </summary>
    /// <param name="preProcessors">
    ///     The sequence of <see cref="IRequestPreProcessor{TRequest}" /> instances to run before the
    ///     handler.
    /// </param>
    public RequestPreProcessorBehavior(IEnumerable<IRequestPreProcessor<TRequest>> preProcessors)
        => _preProcessors = preProcessors;

    /// <summary>
    ///     Executes all registered <see cref="IRequestPreProcessor{TRequest}" /> instances for the incoming request,
    ///     then invokes the next delegate in the pipeline.
    /// </summary>
    /// <param name="request">The incoming request.</param>
    /// <param name="next">The next delegate to invoke in the pipeline, typically the request handler.</param>
    /// <param name="cancellationToken">A token to observe while waiting for the task to complete.</param>
    /// <returns>A task representing the asynchronous operation, containing the response value.</returns>
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        if (next == null) throw new ArgumentNullException(nameof(next));

        foreach (var processor in _preProcessors)
            await processor.Process(request, cancellationToken).ConfigureAwait(false);

        return await next(cancellationToken).ConfigureAwait(false);
    }
}