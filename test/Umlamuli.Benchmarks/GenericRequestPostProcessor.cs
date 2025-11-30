using System.IO;
using System.Threading;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Umlamuli.Pipeline;

namespace Umlamuli.Benchmarks;

[UsedImplicitly]
public class GenericRequestPostProcessor<TRequest, TResponse> : IRequestPostProcessor<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly TextWriter _writer;

    public GenericRequestPostProcessor(TextWriter writer)
    {
        _writer = writer;
    }

    public Task Process(TRequest request, TResponse response, CancellationToken cancellationToken)
    {
        return _writer.WriteLineAsync("- All Done");
    }
}