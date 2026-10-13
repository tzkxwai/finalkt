using System.Diagnostics;
using System.Text.Json;
using Serilog;
using Serilog.Context;

public sealed class TaskManagerService
{
    private readonly string _path;
    private readonly TraceSource _trace;
    private readonly List<TaskItem> _tasks;
    private int _nextId;
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public TaskManagerService(string path, TraceSource trace)
    {
        _path = path;
        _trace = trace;
        using var operation = new OperationScope(_trace, "LoadTasks");
        _tasks = File.Exists(_path)
            ? JsonSerializer.Deserialize<List<TaskItem>>(File.ReadAllText(_path)) ?? new List<TaskItem>()
            : new List<TaskItem>();
        _nextId = _tasks.Count == 0 ? 1 : checked(_tasks.Max(t => t.Id) + 1);
        Log.Information("Загружено задач: {Count}", _tasks.Count);
    }

    public void AddTask(string? title, string description, int priority)
    {
        using var operation = new OperationScope(_trace, "AddTask");
        try
        {
            if (string.IsNullOrWhiteSpace(title))
                throw new ArgumentException("Название задачи не может быть пустым", nameof(title));
            if (priority is < 1 or > 3)
                throw new ArgumentOutOfRangeException(nameof(priority), "Приоритет должен быть от 1 до 3");

            var task = new TaskItem
            {
                Id = _nextId,
                Title = title.Trim(),
                Description = description.Trim(),
                Priority = priority
            };
            _tasks.Add(task);
            try { Save(); }
            catch { _tasks.Remove(task); throw; }
            _nextId++;
            Log.Information("Добавлена задача {TaskTitle}; данные {@Task}", task.Title, task);
            Console.WriteLine($"Задача #{task.Id} добавлена.");
        }
        catch (ArgumentException ex)
        {
            operation.MarkWarning();
            Log.Error(ex, "Ошибка добавления задачи: {TaskTitle}", title);
            Console.WriteLine("Произошла ошибка. Подробнее см. логи.");
        }
        catch (Exception ex)
        {
            operation.MarkError();
            Log.Error(ex, "Ошибка добавления задачи: {TaskTitle}", title);
            Console.WriteLine("Произошла ошибка. Подробнее см. логи.");
        }
    }

    public void RemoveTask(int taskId)
    {
        using var operation = new OperationScope(_trace, "RemoveTask");
        using var context = LogContext.PushProperty("TaskId", taskId);
        try
        {
            var index = _tasks.FindIndex(t => t.Id == taskId);
            if (index < 0)
            {
                operation.MarkWarning();
                Log.Error("Ошибка: задача не найдена. TaskId={TaskId}", taskId);
                Console.WriteLine("Задача не найдена.");
                return;
            }
            var task = _tasks[index];
            _tasks.RemoveAt(index);
            try { Save(); }
            catch { _tasks.Insert(index, task); throw; }
            Log.Information("Задача \"{TaskTitle}\" удалена; TaskId={TaskId}", task.Title, task.Id);
            Console.WriteLine("Задача удалена.");
        }
        catch (Exception ex)
        {
            operation.MarkError();
            Log.Error(ex, "Ошибка при удалении задачи {TaskId}", taskId);
            Console.WriteLine("Произошла ошибка. Подробнее см. логи.");
        }
    }

    public void ListTasks()
    {
        using var operation = new OperationScope(_trace, "ListTasks");
        try
        {
            if (_tasks.Count == 0) Console.WriteLine("Список задач пуст.");
            foreach (var task in _tasks)
                Console.WriteLine($"#{task.Id} | {task.Title} | {task.Description} | Приоритет: {task.Priority} | {task.CreatedAtUtc:yyyy-MM-dd HH:mm} UTC");
            Log.Information("Показан список из {Count} задач", _tasks.Count);
        }
        catch (Exception ex)
        {
            operation.MarkError();
            Log.Error(ex, "Ошибка вывода списка задач");
            Console.WriteLine("Произошла ошибка. Подробнее см. логи.");
        }
    }

    private void Save()
    {
        var tempPath = _path + ".tmp";
        try
        {
            File.WriteAllText(tempPath, JsonSerializer.Serialize(_tasks, JsonOptions));
            File.Move(tempPath, _path, overwrite: true);
            Log.Debug("Коллекция сохранена, всего задач: {Count}", _tasks.Count);
        }
        finally
        {
            if (File.Exists(tempPath)) File.Delete(tempPath);
        }
    }
}
