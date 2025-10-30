using DuckMode.Core.Contracts;
using LiteDB;

namespace DuckMode.Data;

public class LiteDbContext
{
    private readonly Lazy<LiteDatabase> _database;

    public LiteDbContext(string filePath)
    {
        _database = new Lazy<LiteDatabase>(() => new LiteDatabase(filePath));
    }

    public LiteDatabase Database => _database.Value;
}

public class TaskRepository : ITaskRepository
{
    private readonly LiteDbContext _ctx;
    public TaskRepository(LiteDbContext ctx) { _ctx = ctx; }

    public Task UpsertAsync(TaskItem task, CancellationToken cancellationToken)
    {
        var col = _ctx.Database.GetCollection<TaskItem>("tasks");
        col.Upsert(task);
        return Task.CompletedTask;
    }

    public Task<TaskItem?> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        var col = _ctx.Database.GetCollection<TaskItem>("tasks");
        return Task.FromResult<TaskItem?>(col.FindById(id));
    }

    public Task<IReadOnlyList<TaskItem>> GetUpcomingAsync(DateTime until, CancellationToken cancellationToken)
    {
        var col = _ctx.Database.GetCollection<TaskItem>("tasks");
        var list = col.Query()
            .Where(t => t.DueAt != null && t.DueAt <= until && t.Status == DuckMode.Core.Contracts.TaskStatus.Pending)
            .ToList()
            .AsReadOnly();
        return Task.FromResult<IReadOnlyList<TaskItem>>(list);
    }
}

public class ReminderRepository : IReminderRepository
{
    private readonly LiteDbContext _ctx;
    public ReminderRepository(LiteDbContext ctx) { _ctx = ctx; }

    public Task UpsertAsync(Reminder reminder, CancellationToken cancellationToken)
    {
        var col = _ctx.Database.GetCollection<Reminder>("reminders");
        col.Upsert(reminder);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<Reminder>> GetPendingAsync(DateTime now, CancellationToken cancellationToken)
    {
        var col = _ctx.Database.GetCollection<Reminder>("reminders");
        var list = col.Query()
            .Where(r => r.TriggerAt <= now && !r.Sent)
            .ToList()
            .AsReadOnly();
        return Task.FromResult<IReadOnlyList<Reminder>>(list);
    }
}

public class ConversationRepository : IConversationRepository
{
    private readonly LiteDbContext _ctx;
    public ConversationRepository(LiteDbContext ctx) { _ctx = ctx; }

    public Task AddAsync(ConversationMessage message, CancellationToken cancellationToken)
    {
        var col = _ctx.Database.GetCollection<ConversationMessage>("conversations");
        col.Insert(message);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<ConversationMessage>> GetRecentAsync(int take, CancellationToken cancellationToken)
    {
        var col = _ctx.Database.GetCollection<ConversationMessage>("conversations");
        var list = col.Query().OrderByDescending(m => m.CreatedAt).Limit(take).ToList().AsReadOnly();
        return Task.FromResult<IReadOnlyList<ConversationMessage>>(list);
    }
}

public class SettingsService : ISettingsService
{
    private readonly LiteDbContext _ctx;
    public SettingsService(LiteDbContext ctx) { _ctx = ctx; }

    public Task<AppSettings> GetAsync(CancellationToken cancellationToken)
    {
        var col = _ctx.Database.GetCollection<AppSettings>("settings");
        var settings = col.FindAll().FirstOrDefault() ?? new AppSettings();
        return Task.FromResult(settings);
    }

    public Task SaveAsync(AppSettings settings, CancellationToken cancellationToken)
    {
        var col = _ctx.Database.GetCollection<AppSettings>("settings");
        col.DeleteAll();
        col.Insert(settings);
        return Task.CompletedTask;
    }
}



