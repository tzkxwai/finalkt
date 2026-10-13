using System.Diagnostics;
using Serilog;
using Serilog.Context;

public sealed class OperationScope : IDisposable
{
    private readonly TraceSource _source;
    private readonly Stopwatch _watch;
    private readonly IDisposable? _operationProperty;
    private readonly IDisposable? _idProperty;
    private bool _disposed;
    public string Name { get; }
    public string Id { get; } = Guid.NewGuid().ToString("N");
    public string Result { get; private set; } = "Success";

    public OperationScope(TraceSource source, string name)
    {
        _source = source;
        Name = name;
        _operationProperty = LogContext.PushProperty("Operation", name);
        _idProperty = LogContext.PushProperty("OperationId", Id);
        _source.TraceEvent(TraceEventType.Start, 100, "START {0}, OperationId={1}", Name, Id);
        _source.Flush();
        _watch = Stopwatch.StartNew();
        Log.Debug("Начало операции {OperationName}", Name);
    }

    public void MarkError() => Result = "Error";
    public void MarkWarning() => Result = "Warning";

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _watch.Stop();
        _source.TraceEvent(TraceEventType.Stop, 101,
            "STOP {0}, OperationId={1}, Result={2}, ElapsedMs={3:F3}",
            Name, Id, Result, _watch.Elapsed.TotalMilliseconds);
        _source.Flush();
        Log.Information("Операция {OperationName} завершена: {Result}; время {ElapsedMs:F3} мс",
            Name, Result, _watch.Elapsed.TotalMilliseconds);
        _idProperty?.Dispose();
        _operationProperty?.Dispose();
    }
}
