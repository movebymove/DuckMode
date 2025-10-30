using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using DuckMode.Core.Contracts;
using Microsoft.Extensions.DependencyInjection;

namespace DuckMode.App;

public class ChatMessage
{
    public string Sender { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string SenderColor { get; set; } = "#333";
    public string Background { get; set; } = "#F5F5F5";
}

public partial class MainWindow : Window
{
    private readonly IAiClient _ai;
    private readonly ITaskRepository _tasks;
    private readonly IReminderScheduler _scheduler;
    private readonly INlpTaskExtractor _extractor;

    public ObservableCollection<ChatMessage> Messages { get; } = new();

    private bool _awaitingDeadline;
    private string? _pendingTitle;
    private bool _awaitingWaterTime;

    public MainWindow()
    {
        InitializeComponent();
        _ai = App.HostInstance!.Services.GetRequiredService<IAiClient>();
        _tasks = App.HostInstance!.Services.GetRequiredService<ITaskRepository>();
        _scheduler = App.HostInstance!.Services.GetRequiredService<IReminderScheduler>();
        _extractor = App.HostInstance!.Services.GetRequiredService<INlpTaskExtractor>();

        MessagesList.ItemsSource = Messages;
        SendButton.Click += SendButton_Click;
        Loaded += OnLoaded;
    }

    private void OnLoaded(object? sender, RoutedEventArgs e)
    {
        // Snap to bottom-right
        var workArea = SystemParameters.WorkArea;
        this.Left = workArea.Right - this.Width - 16;
        this.Top = workArea.Bottom - this.Height - 16;
        AddBotMessage("Chào bạn, hôm nay bạn thế nào?");

        // Bắt đầu animation vịt chạy qua lại
        var anim = new System.Windows.Media.Animation.DoubleAnimation
        {
            From = 0,
            To = 260,
            Duration = new Duration(TimeSpan.FromSeconds(3)),
            AutoReverse = true,
            RepeatBehavior = System.Windows.Media.Animation.RepeatBehavior.Forever
        };
        if (FindName("PsyduckRunTransform") is System.Windows.Media.TranslateTransform tf)
        {
            tf.BeginAnimation(System.Windows.Media.TranslateTransform.XProperty, anim);
        }
    }

    private void SendButton_Click(object sender, RoutedEventArgs e)
    {
        _ = OnSendAsync();
    }

    private void InputBox_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == System.Windows.Input.Key.Enter)
        {
            _ = OnSendAsync();
            e.Handled = true;
        }
    }

    private void MinimizeButton_Click(object sender, RoutedEventArgs e)
    {
        this.WindowState = WindowState.Minimized;
    }

    private void TitleBar_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (e.ChangedButton == System.Windows.Input.MouseButton.Left)
        {
            this.DragMove();
        }
    }

    private void AddUserMessage(string content)
    {
        Messages.Add(new ChatMessage 
        { 
            Sender = "Bạn", 
            Content = content, 
            SenderColor = "#0066CC",
            Background = "#E3F2FD"
        });
        ScrollToBottom();
    }

    private void AddBotMessage(string content)
    {
        Messages.Add(new ChatMessage 
        { 
            Sender = "🦆 Vịt", 
            Content = content, 
            SenderColor = "#FF8C00",
            Background = "#FFF8DC"
        });
        ScrollToBottom();
    }

    private void ScrollToBottom()
    {
        // Delay scroll to after UI updates
        Dispatcher.BeginInvoke(new Action(() =>
        {
            ChatScrollViewer.ScrollToEnd();
        }), System.Windows.Threading.DispatcherPriority.Loaded);
    }

    private async Task OnSendAsync()
    {
        var text = InputBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(text)) return;
        AddUserMessage(text);
        InputBox.Clear();

        // Check for reminder prefix /r
        if (text.StartsWith("/r", StringComparison.OrdinalIgnoreCase))
        {
            var reminderText = text.Substring(2).Trim();
            
            // Check for water break intent
            if (IsWaterBreakIntent(reminderText))
            {
                var when = ParseRelativeTime(reminderText);
                if (when is DateTime dt)
                {
                    _scheduler.ScheduleWaterBreakAt(dt);
                    AddBotMessage("✅ Ok, mình sẽ nhắc bạn uống nước đúng giờ đó!");
                    return;
                }
                AddBotMessage("⏰ Bạn muốn mình nhắc uống nước lúc nào? (ví dụ: sau 15 phút / trong 1 giờ)");
                return;
            }

            // Extract task from reminder text
            var now = DateTime.Now;
            var result = _extractor.Extract(reminderText, now, System.Globalization.CultureInfo.GetCultureInfo("vi-VN"));
            if (result.Success && result.Task is not null && result.Task.DueAt is not null)
            {
                if (result.Task.DueAt <= now)
                {
                    AddBotMessage("⛔ Lời nhắc không hợp lệ vì thời điểm đã qua. Bạn hãy nhập lại mốc thời gian ở tương lai nhé!");
                    return;
                }
                await _tasks.UpsertAsync(result.Task, CancellationToken.None);
                _scheduler.ScheduleTask(result.Task);
                
                // Show detailed time info
                var minutesUntilDue = (int)Math.Max(0, (result.Task.DueAt.Value - now).TotalMinutes);
                var timeInfo = $"⏰ Hiện tại: {now:HH:mm:ss}\n" +
                              $"📅 Deadline: {result.Task.DueAt.Value:HH:mm:ss}\n" +
                              $"⏱️ Còn lại: {minutesUntilDue} phút\n" +
                              $"🔔 Nhắc trước: {result.Task.RemindBeforeMinutes} phút";
                
                var msg = result.Task.RemindBeforeMinutes > 0
                    ? $"✅ Đã lưu reminder '{result.Task.Title}' và sẽ nhắc trước {result.Task.RemindBeforeMinutes} phút.\n\n{timeInfo}"
                    : $"✅ Đã lưu reminder '{result.Task.Title}'. Vì thời gian rất gần, mình sẽ nhắc ngay nhé!\n\n{timeInfo}";
                AddBotMessage(msg);
                return;
            }
            
            if (result.MissingField == "due" && result.Task is not null && !string.IsNullOrWhiteSpace(result.Task.Title))
            {
                if (IsMeaningfulTaskTitle(result.Task.Title))
                {
                    _pendingTitle = result.Task.Title;
                    _awaitingDeadline = true;
                    AddBotMessage($"⏰ Deadline cho '{_pendingTitle}' là lúc nào? (ví dụ: hôm nay 16:00)");
                    return;
                }
            }
            
            AddBotMessage("❌ Mình chưa hiểu thời gian. Ví dụ: '/r nhắc tôi đi họp sau 10 phút' hoặc '/r nhắc tôi gọi điện lúc 15:30'");
            return;
        }

        // Water break intent
        if (IsWaterBreakIntent(text))
        {
            var when = ParseRelativeTime(text);
            if (when is DateTime dt)
            {
                _scheduler.ScheduleWaterBreakAt(dt);
                AddBotMessage("Ok, mình sẽ nhắc bạn uống nước đúng giờ đó!");
                return;
            }
            _awaitingWaterTime = true;
            AddBotMessage("Bạn muốn mình nhắc uống nước lúc nào? (ví dụ: sau 15 phút / trong 1 giờ)");
            return;
        }

        if (_awaitingDeadline && _pendingTitle is not null)
        {
            var parsed = _extractor.Extract(text, DateTime.Now, System.Globalization.CultureInfo.GetCultureInfo("vi-VN"));
            if (parsed.Task?.DueAt is DateTime due)
            {
                var task = new TaskItem { Title = _pendingTitle, DueAt = due, RemindBeforeMinutes = 30 };
                await _tasks.UpsertAsync(task, CancellationToken.None);
                _scheduler.ScheduleTask(task);
                _awaitingDeadline = false;
                _pendingTitle = null;
                AddBotMessage("Đã lưu task và đặt nhắc trước 30 phút. Cố lên nhé!");
                return;
            }
            AddBotMessage("Mình chưa hiểu thời gian. Bạn có thể nói dạng 'hôm nay 16:00' hoặc 'mai 9h'?");
            return;
        }

        // For normal chat, just use AI (no automatic task extraction)

        if (_awaitingWaterTime)
        {
            var when2 = ParseRelativeTime(text);
            if (when2 is DateTime dt2)
            {
                _scheduler.ScheduleWaterBreakAt(dt2);
                _awaitingWaterTime = false;
                AddBotMessage("Đã đặt nhắc uống nước!");
                return;
            }
            AddBotMessage("Mình chưa hiểu thời gian. Ví dụ: 'sau 10 phút' hoặc 'trong 2 giờ'.");
            return;
        }

        try
        {
            var response = await _ai.ChatAsync(new[] { new AiMessage("user", text) }, CancellationToken.None);
            AddBotMessage(response.Content);
        }
        catch (Exception ex)
        {
            // Log exception for debugging
            System.Diagnostics.Debug.WriteLine($"Chat error: {ex.GetType().Name} - {ex.Message}");
            if (ex.InnerException != null)
            {
                System.Diagnostics.Debug.WriteLine($"Inner exception: {ex.InnerException.GetType().Name} - {ex.InnerException.Message}");
            }
            
            // Check if it's a connection/network error
            var isConnectionError = ex.Message.Contains("Failed to connect") || 
                                   ex.Message.Contains("Connection") ||
                                   ex.Message.Contains("timeout") ||
                                   ex.Message.Contains("timed out") ||
                                   ex.Message.Contains("refused") ||
                                   ex.InnerException?.Message?.Contains("Connection") == true ||
                                   ex.InnerException?.Message?.Contains("refused") == true;
            
            // Check for memory error
            if (ex.Message.Contains("memory") || ex.Message.Contains("RAM") || ex.Message.Contains("requires more system memory"))
            {
                AddBotMessage("❌ Model AI hiện tại cần quá nhiều RAM. Mình sẽ quay lại model nhỏ hơn (qwen2.5:7b).");
                AddBotMessage("💡 Mẹo: Để dùng model lớn hơn, bạn cần máy có nhiều RAM hơn (16GB+).");
                var echo = new DuckMode.AI.StubAiClient();
                var stubResponse = await echo.ChatAsync(new[] { new AiMessage("user", text) }, CancellationToken.None);
                AddBotMessage(stubResponse.Content);
                return;
            }
            
            if (isConnectionError)
            {
                AddBotMessage("Xin lỗi, mình chưa kết nối được AI local. Đang thử khởi động lại Ollama...");
                
                // Try one more time with longer wait
                await Task.Delay(3000);
                try
                {
                    var retryResponse = await _ai.ChatAsync(new[] { new AiMessage("user", text) }, CancellationToken.None);
                    AddBotMessage(retryResponse.Content);
                    return;
                }
                catch (Exception retryEx)
                {
                    System.Diagnostics.Debug.WriteLine($"Retry failed: {retryEx.Message}");
                    AddBotMessage("Vẫn chưa kết nối được. Đảm bảo Ollama đang chạy (kiểm tra bằng 'ollama list' trong terminal).");
                    var echo = new DuckMode.AI.StubAiClient();
                    var stubResponse = await echo.ChatAsync(new[] { new AiMessage("user", text) }, CancellationToken.None);
                    AddBotMessage(stubResponse.Content);
                }
            }
            else
            {
                // Other error - show simplified message and use stub
                AddBotMessage("Có lỗi khi xử lý yêu cầu. Mình sẽ phản hồi đơn giản nhé.");
                var echo = new DuckMode.AI.StubAiClient();
                var stubResponse = await echo.ChatAsync(new[] { new AiMessage("user", text) }, CancellationToken.None);
                AddBotMessage(stubResponse.Content);
            }
        }
    }

    private static bool IsWaterBreakIntent(string text)
    {
        var lower = text.ToLowerInvariant();
        return lower.Contains("uống nước") || lower.Contains("uong nuoc") || lower.Contains("drink water");
    }

    private static bool HasTaskIntent(string text)
    {
        var lower = text.ToLowerInvariant();
        
        // If it's a question, NOT a task
        var questionWords = new[] { "gì", "sao", "tại sao", "bao nhiêu", "khi nào", "ở đâu", "như thế nào", "là ai", "là gì", "ngày nào", "tháng nào", "năm nào" };
        var hasQuestionMark = text.Contains("?") || text.Contains("？");
        var isQuestion = hasQuestionMark || questionWords.Any(q => lower.Contains(q));
        if (isQuestion) return false;
        
        // Strong task action verbs - if these are present, it's likely a task
        var strongTaskVerbs = new[]
        {
            "nộp", "gửi", "họp", "gặp", "hẹn", "làm", "hoàn thành", "submit",
            "báo cáo", "bài tập", "deadline", "hạn chót"
        };
        var hasStrongVerb = strongTaskVerbs.Any(v => lower.Contains(v));
        
        // Time patterns - must have actual time/deadline
        var hasSpecificTime = System.Text.RegularExpressions.Regex.IsMatch(lower, 
            "\\b\\d{1,2}(h|:)\\d{0,2}\\b|\\b(sau|trong)\\s+\\d+\\s+(phút|giờ)\\b");
        
        var hasRelativeTime = System.Text.RegularExpressions.Regex.IsMatch(lower, 
            "\\b(hôm nay|mai|ngày mai|chiều|sáng|tối)\\s+\\d|\\b(tuần|tháng)\\s+(sau|này)");
        
        // Only consider as task if:
        // 1. Has strong task verb + any time, OR
        // 2. Has specific time + action context, OR
        // 3. Explicitly mentions "task", "việc", "công việc", "nhiệm vụ"
        var explicitTaskWord = new[] { "task", "việc", "công việc", "nhiệm vụ", "dự án" };
        var hasExplicitTask = explicitTaskWord.Any(w => lower.Contains(w));
        
        if (hasExplicitTask) return true;
        if (hasStrongVerb && (hasSpecificTime || hasRelativeTime)) return true;
        if (hasStrongVerb && (lower.Contains("phải") || lower.Contains("cần"))) return true;
        
        return false;
    }

    private static bool IsMeaningfulTaskTitle(string title)
    {
        if (string.IsNullOrWhiteSpace(title)) return false;
        
        var lower = title.ToLowerInvariant().Trim();
        var len = lower.Length;
        
        // Single words that are likely greetings/chat
        var greetings = new[] { "hi", "hello", "xin chào", "chào", "hey", "ok", "oke", "okay", "ừ", "vâng", "có", "không" };
        if (greetings.Contains(lower)) return false;
        
        // Very short titles (< 3 chars) are likely not tasks
        if (len < 3) return false;
        
        // If it's just numbers or single char, not meaningful
        if (System.Text.RegularExpressions.Regex.IsMatch(lower, "^[\\d\\s]+$")) return false;
        
        return true;
    }

    private static DateTime? ParseRelativeTime(string text)
    {
        var lower = text.ToLowerInvariant();
        var relMin = System.Text.RegularExpressions.Regex.Match(lower, "\\b(sau|trong)\\s+(?<n>\\d{1,3})\\s*(phut|phút|minute|minutes|mins)\\b");
        if (relMin.Success)
        {
            var n = int.Parse(relMin.Groups["n"].Value);
            return DateTime.Now.AddMinutes(Math.Max(1, n));
        }
        var relHr = System.Text.RegularExpressions.Regex.Match(lower, "\\b(sau|trong)\\s+(?<n>\\d{1,2})\\s*(gio|giờ|hour|hours|h)\\b");
        if (relHr.Success)
        {
            var n = int.Parse(relHr.Groups["n"].Value);
            return DateTime.Now.AddHours(Math.Max(1, n));
        }
        return null;
    }
}