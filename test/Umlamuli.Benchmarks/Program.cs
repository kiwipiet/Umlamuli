using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Environments;
using BenchmarkDotNet.Exporters;
using BenchmarkDotNet.Exporters.Csv;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Loggers;
using BenchmarkDotNet.Running;

namespace Umlamuli.Benchmarks;

public class MultiRuntimeConfig : ManualConfig
{
    public MultiRuntimeConfig()
    {
        // These settings show that by upgrading to newer runtimes gives significant performance improvements.

        // Runtimes
        AddJob(Job.Default.WithRuntime(ClrRuntime.Net462));
        AddJob(Job.Default.WithRuntime(CoreRuntime.Core80));
        AddJob(Job.Default.WithRuntime(CoreRuntime.Core90));
        AddJob(Job.Default.WithRuntime(CoreRuntime.Core10_0));

        // Live output
        AddLogger(ConsoleLogger.Unicode);

        // Columns
        AddColumnProvider(DefaultColumnProviders.Instance);

        // Exporters (pick any you want)
        AddExporter(MarkdownExporter.Default);
        AddExporter(HtmlExporter.Default);
        AddExporter(CsvExporter.Default);
        // Optional plots (requires R installed):
        // AddExporter(RPlotExporter.Default);

        AddDiagnoser(MemoryDiagnoser.Default);
        //AddDiagnoser(new InliningDiagnoser());
        //AddDiagnoser(new EtwProfiler());
        //AddDiagnoser(ThreadingDiagnoser.Default);
        //AddDiagnoser(ExceptionDiagnoser.Default);

        // Combine results if you run multiple times:
        WithOptions(ConfigOptions.JoinSummary);
    }
}

public class Program
{
    public static void Main(string[] args) =>
        BenchmarkRunner.Run<Benchmarks>(new MultiRuntimeConfig());
}