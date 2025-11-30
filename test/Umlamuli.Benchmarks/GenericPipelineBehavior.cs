using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Umlamuli.Benchmarks;

public class GenericPipelineBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly TextWriter _writer;

    public GenericPipelineBehavior(TextWriter writer)
    {
        _writer = writer;
    }

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        if (next == null) throw new ArgumentNullException(nameof(next));
        await _writer.WriteLineAsync("-- Handling Request");
        var response = await next(cancellationToken);
        await _writer.WriteLineAsync("-- Finished Request");
        return response;
    }
}