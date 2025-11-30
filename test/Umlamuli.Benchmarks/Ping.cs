using System.Threading;
using System.Threading.Tasks;
using JetBrains.Annotations;

namespace Umlamuli.Benchmarks;

public class Ping : IRequest
{
    public string Message { get; set; }
}

[UsedImplicitly]
public class PingHandler : IRequestHandler<Ping>
{
    public Task Handle(Ping request, CancellationToken cancellationToken) => Task.CompletedTask;
}