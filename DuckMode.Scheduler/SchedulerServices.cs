using DuckMode.Core.Contracts;

namespace DuckMode.Scheduler;

public class ReminderScheduler : IReminderScheduler, IDisposable
{
    private readonly IReminderRepository _reminders;
    private readonly ITaskRepository _tasks;
    private readonly INotificationService _notifications;
    private readonly Timer _pollTimer;
    private int _breakMinutes = 60;
    private DateTime _nextBreakAt;
    private bool _nextIsMoveBreak = false;

    public ReminderScheduler(IReminderRepository reminders, ITaskRepository tasks, INotificationService notifications)
    {
        _reminders = reminders;
        _tasks = tasks;
        _notifications = notifications;
        _pollTimer = new Timer(async _ => await PollAsync().ConfigureAwait(false), null, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(20));
        _nextBreakAt = DateTime.Now.AddMinutes(_breakMinutes);
    }

    public void ScheduleTask(TaskItem task)
    {
        if (task.DueAt is null) return;
        var trigger = task.DueAt.Value.AddMinutes(-Math.Max(0, task.RemindBeforeMinutes));
        if (trigger <= DateTime.Now)
        {
            trigger = DateTime.Now.AddSeconds(5);
        }
        var reminder = new Reminder
        {
            TaskId = task.Id,
            TriggerAt = trigger,
            Type = ReminderType.Deadline,
            Sent = false
        };
        _ = _reminders.UpsertAsync(reminder, CancellationToken.None);
    }

    public void CancelForTask(Guid taskId)
    {
        // Future: implement
    }

    public void ScheduleWaterBreak(int intervalMinutes)
    {
        _breakMinutes = Math.Max(10, intervalMinutes);
        _nextBreakAt = DateTime.Now.AddMinutes(_breakMinutes);
        _nextIsMoveBreak = false;
    }

    public void ScheduleWaterBreakAt(DateTime when)
    {
        _nextBreakAt = when;
        _nextIsMoveBreak = false;
    }

    public void ScheduleBreaks(int intervalMinutes)
    {
        _breakMinutes = Math.Max(10, intervalMinutes);
        _nextBreakAt = DateTime.Now.AddMinutes(_breakMinutes);
        _nextIsMoveBreak = false;
    }

    private async Task PollAsync()
    {
        var now = DateTime.Now;
        var due = await _reminders.GetPendingAsync(now, CancellationToken.None).ConfigureAwait(false);
        foreach (var r in due)
        {
            if (r.Type == ReminderType.Deadline && r.TaskId.HasValue)
            {
                var task = await _tasks.GetAsync(r.TaskId.Value, CancellationToken.None);
                if (task != null)
                {
                    _notifications.ShowDeadline(task);
                }
            }
            else if (r.Type == ReminderType.WaterBreak)
            {
                _notifications.ShowWaterBreak();
            }
            r.Sent = true;
            await _reminders.UpsertAsync(r, CancellationToken.None).ConfigureAwait(false);
        }

        if (now >= _nextBreakAt)
        {
            if (_nextIsMoveBreak)
                _notifications.ShowMoveBreak();
            else
                _notifications.ShowWaterBreak();
            _nextIsMoveBreak = !_nextIsMoveBreak;
            _nextBreakAt = DateTime.Now.AddMinutes(_breakMinutes);
        }
    }

    public void Dispose()
    {
        _pollTimer.Dispose();
    }
}



