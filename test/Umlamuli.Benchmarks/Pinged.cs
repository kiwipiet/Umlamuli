using System.Threading;
using System.Threading.Tasks;
using JetBrains.Annotations;

namespace Umlamuli.Benchmarks;

public class Pinged : INotification;

[UsedImplicitly]
public class PingedHandler : INotificationHandler<Pinged>
{
    public Task Handle(Pinged notification, CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}