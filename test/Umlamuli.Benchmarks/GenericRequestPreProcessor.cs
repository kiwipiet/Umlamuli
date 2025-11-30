using System.IO;
using System.Threading;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Umlamuli.Pipeline;

namespace Umlamuli.Benchmarks;

[UsedImplicitly]
public class GenericRequestPreProcessor<TRequest> : IRequestPreProcessor<TRequest>
    where TRequest : notnull
{
    private readonly TextWriter _writer;

    public GenericRequestPreProcessor(TextWriter writer)
    {
        _writer = writer;
    }

    public Task Process(TRequest request, CancellationToken cancellationToken)
    {
        return _writer.WriteLineAsync("- Starting Up");
    }
}