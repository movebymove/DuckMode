using System.Windows;
using DuckMode.Core.Contracts;
using System;

namespace DuckMode.App
{
    public partial class ReminderPopupWindow : Window
    {
        public ReminderPopupWindow(TaskItem? task)
        {
            InitializeComponent();

            string friendly;
            string title = "";
            string timestr = string.Empty;

            if (task != null && !string.IsNullOrWhiteSpace(task.Title))
            {
                title = task.Title.Trim();
            }

            if (task != null && task.DueAt.HasValue)
            {
                timestr = task.DueAt.Value.ToString("HH:mm");
            }

            if (!string.IsNullOrWhiteSpace(title))
            {
                friendly = $"Bạn ơi, sắp đến giờ \"{title}\" rồi! Chuẩn bị đi nào!";
                TaskTextBlock.Text = $"Công việc: {title}";
            }
            else
            {
                friendly = "Sắp đến giờ nhắc nhở rồi, kiểm tra DuckMode để không bỏ lỡ công việc nhé!";
                TaskTextBlock.Text = "";
            }

            CustomFriendlyTextBlock.Text = friendly;
            TimeTextBlock.Text = !string.IsNullOrWhiteSpace(timestr) ? $"⏰ Đến giờ: {timestr}" : "";
        }

        public ReminderPopupWindow(string message)
            : this(new TaskItem { Title = message })
        { }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}
