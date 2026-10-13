using System.Diagnostics;
using System.Text;
using Serilog;
using Serilog.Context;
using Serilog.Events;
using Serilog.Formatting.Compact;

Console.InputEncoding = Encoding.UTF8;
Console.OutputEncoding = Encoding.UTF8;

var logFolder = Path.Combine(AppContext.BaseDirectory, "logs");
Directory.CreateDirectory(logFolder);

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Debug()
    .Enrich.FromLogContext()
    .Enrich.WithMachineName()
    .Enrich.WithProperty("Application", "TaskManager")
    .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
    .WriteTo.File(new CompactJsonFormatter(),
        Path.Combine(logFolder, "taskmanager-.json"),
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 14,
        shared: true)
    .CreateLogger();

var tracer = new TraceSource("TaskManagerTrace", SourceLevels.All);
tracer.Listeners.Clear();
tracer.Listeners.Add(new TextWriterTraceListener(Path.Combine(logFolder, "trace.log"))
{
    TraceOutputOptions = TraceOptions.DateTime | TraceOptions.ProcessId | TraceOptions.ThreadId
});
tracer.Switch.Level = SourceLevels.All;

try
{
    using var session = new OperationScope(tracer, "Session");
    Log.Information("Приложение TaskManager запущено. Сеанс {SessionId}", session.Id);
    var manager = new TaskManagerService(Path.Combine(AppContext.BaseDirectory, "tasks.json"), tracer);
    RunMenu(manager);
    Log.Information("Программа завершается");
}
catch (Exception ex)
{
    Log.Fatal(ex, "Необработанная ошибка");
    Console.WriteLine("Произошла ошибка. Подробнее см. логи.");
    Environment.ExitCode = 1;
}
finally
{
    tracer.Flush();
    tracer.Close();
    Log.CloseAndFlush();
}

static void RunMenu(TaskManagerService manager)
{
    while (true)
    {
        Console.WriteLine("\n=== TASKMANAGER ===");
        Console.WriteLine("1. Добавить задачу");
        Console.WriteLine("2. Удалить задачу по ID");
        Console.WriteLine("3. Показать задачи");
        Console.WriteLine("4. Выход");
        Console.Write("Команда: ");
        string? command = Console.ReadLine()?.Trim().ToLowerInvariant();
        if (command is null or "4" or "exit") return;
        switch (command)
        {
            case "1":
            case "add":
                Console.Write("Название: ");
                var title = Console.ReadLine();
                Console.Write("Описание (можно оставить пустым): ");
                var description = Console.ReadLine() ?? "";
                Console.Write("Приоритет (1 - низкий, 2 - средний, 3 - высокий; Enter = 2): ");
                var input = Console.ReadLine();
                var priority = string.IsNullOrWhiteSpace(input) ? 2
                    : int.TryParse(input, out var parsed) ? parsed : 0;
                manager.AddTask(title, description, priority);
                break;
            case "2":
            case "remove":
                Console.Write("ID задачи: ");
                var idInput = Console.ReadLine();
                if (int.TryParse(idInput, out var id)) manager.RemoveTask(id);
                else
                {
                    Log.Warning("Некорректный ID: {Input}", idInput);
                    Console.WriteLine("ID должен быть целым числом.");
                }
                break;
            case "3":
            case "list":
                manager.ListTasks();
                break;
            default:
                Log.Warning("Неизвестная команда: {Command}", command);
                Console.WriteLine("Неизвестная команда.");
                break;
        }
    }
}

public sealed class TaskItem
{
    public int Id { get; set; }
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public int Priority { get; set; } = 2;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
