using System.Windows;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using DuckMode.Core.Contracts;

namespace DuckMode.App
{
    public partial class PsyduckReminderPopup : Window
    {
        private readonly DispatcherTimer _autoCloseTimer;
        public PsyduckReminderPopup(string text, string time)
        {
            InitializeComponent();
            BubbleText.Text = text;
            BubbleTime.Text = string.IsNullOrWhiteSpace(time) ? "" : $"⏰ Đến giờ: {time}";

            // Fade-in animation
            this.Opacity = 0;
            Loaded += (s, e) => {
                var fade = new DoubleAnimation(0, 1, new Duration(TimeSpan.FromSeconds(0.6)));
                this.BeginAnimation(Window.OpacityProperty, fade);
            };
            // Vị trí góc phải trên màn hình
            var desktop = System.Windows.SystemParameters.WorkArea;
            this.Left = desktop.Right - this.Width - 20;
            this.Top = desktop.Top + 16;

            // Tự động đóng sau 30s
            _autoCloseTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(30) };
            _autoCloseTimer.Tick += (s, e) => { _autoCloseTimer.Stop(); CloseMe(); };
            _autoCloseTimer.Start();
        }
        public PsyduckReminderPopup(TaskItem? task)
            : this(
                (!string.IsNullOrWhiteSpace(task?.Title)
                    ? (task?.RemindBeforeMinutes == 0
                        ? $"Bạn ơi, đã đến giờ '{task.Title}' rồi! Bắt đầu thôi nào!"
                        : $"Bạn ơi, sắp đến giờ '{task.Title}' rồi! Chuẩn bị đi nào!")
                    : (task?.RemindBeforeMinutes == 0
                        ? "Đã đến giờ nhắc nhở rồi, kiểm tra DuckMode để không bỏ lỡ công việc nhé!"
                        : "Sắp đến giờ nhắc nhở rồi, kiểm tra DuckMode để không bỏ lỡ công việc nhé!")),
                task?.DueAt is DateTime dt ? dt.ToString("HH:mm") : "")
        { }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            _autoCloseTimer.Stop();
            CloseMe();
        }
        private void CloseMe()
        {
            // Fade-out rồi đóng
            var fadout = new DoubleAnimation(1, 0, new Duration(TimeSpan.FromSeconds(0.45)));
            fadout.Completed += (s, e) => this.Close();
            this.BeginAnimation(Window.OpacityProperty, fadout);
        }
    }
}


