using System.Globalization;

namespace DuckMode.Core.Contracts;

public interface IAiClient
{
    Task<AiResponse> ChatAsync(IEnumerable<AiMessage> history, CancellationToken cancellationToken);
}

public interface INlpTaskExtractor
{
    ExtractResult Extract(string naturalText, DateTime now, CultureInfo culture);
}

public interface IReminderScheduler
{
    void ScheduleTask(TaskItem task);
    void CancelForTask(Guid taskId);
    void ScheduleWaterBreak(int intervalMinutes);
    void ScheduleWaterBreakAt(DateTime when);
}

public enum WaterBreakAction { Dismiss, Snooze15 }

public interface INotificationService
{
    void ShowDeadline(TaskItem task);
    WaterBreakAction ShowWaterBreak();
    void ShowMoveBreak();
}

public interface ITaskRepository
{
    Task UpsertAsync(TaskItem task, CancellationToken cancellationToken);
    Task<TaskItem?> GetAsync(Guid id, CancellationToken cancellationToken);
    Task<IReadOnlyList<TaskItem>> GetUpcomingAsync(DateTime until, CancellationToken cancellationToken);
}

public interface IReminderRepository
{
    Task UpsertAsync(Reminder reminder, CancellationToken cancellationToken);
    Task<IReadOnlyList<Reminder>> GetPendingAsync(DateTime now, CancellationToken cancellationToken);
}

public interface IConversationRepository
{
    Task AddAsync(ConversationMessage message, CancellationToken cancellationToken);
    Task<IReadOnlyList<ConversationMessage>> GetRecentAsync(int take, CancellationToken cancellationToken);
}

public interface ISettingsService
{
    Task<AppSettings> GetAsync(CancellationToken cancellationToken);
    Task SaveAsync(AppSettings settings, CancellationToken cancellationToken);
}

public record AiMessage(string Role, string Content);

public record AiResponse(string Content);

public record ExtractResult(bool Success, TaskItem? Task, string? MissingField);

public enum TaskPriority { Low = 0, Normal = 1, High = 2 }

public enum TaskStatus { Pending = 0, Done = 1 }

public class TaskItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Title { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public DateTime? DueAt { get; set; }
    public int RemindBeforeMinutes { get; set; } = 30;
    public TaskPriority Priority { get; set; } = TaskPriority.Normal;
    public TaskStatus Status { get; set; } = TaskStatus.Pending;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public enum ReminderType { Deadline, WaterBreak, MoveBreak }

public class Reminder
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? TaskId { get; set; }
    public DateTime TriggerAt { get; set; }
    public ReminderType Type { get; set; }
    public bool Sent { get; set; }
}

public class ConversationMessage
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Role { get; set; } = "user";
    public string Content { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public Guid? TaskLinkedId { get; set; }
}

public class AppSettings
{
    public string AiProvider { get; set; } = "OpenAI";
    public string? ApiKeyEncrypted { get; set; }
    public int WaterBreakMinutes { get; set; } = 60;
    public bool StartupOnBoot { get; set; } = false;
    public bool PrivacyMode { get; set; } = false;
}



