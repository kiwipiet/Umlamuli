using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Umlamuli.Examples.ExceptionHandler;
using Umlamuli.Pipeline;

public class LogExceptionAction : IRequestExceptionAction<Ping, Exception>
{
    private readonly TextWriter _writer;

    public LogExceptionAction(TextWriter writer) => _writer = writer;

    public Task Execute(Ping request, Exception exception, CancellationToken cancellationToken) 
        => _writer.WriteLineAsync($"--- Exception: '{exception.GetType().FullName}'");
}