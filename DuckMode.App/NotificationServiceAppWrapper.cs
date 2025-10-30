using DuckMode.Core.Contracts;
using System.Windows;
using System;

namespace DuckMode.App
{
    public class NotificationServiceAppWrapper : INotificationService
    {
        private readonly INotificationService _inner;
        public NotificationServiceAppWrapper(INotificationService inner)
        {
            _inner = inner;
        }

        public void ShowDeadline(TaskItem task)
        {
            // Show Psyduck style popup (comic, top right)
            Application.Current.Dispatcher.Invoke(() =>
            {
                var psyduck = new PsyduckReminderPopup(task);
                psyduck.Show();
            });
            _inner.ShowDeadline(task);
        }

        public WaterBreakAction ShowWaterBreak()
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                var psyduck = new PsyduckReminderPopup("Bạn ơi, đã đến giờ uống nước rồi! Cùng tiếp nước nào!", "");
                psyduck.Show();
            });
            return WaterBreakAction.Dismiss;
        }

        public void ShowMoveBreak()
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                var psyduck = new PsyduckReminderPopup("Đã đến giờ vận động rồi! Dậy vươn vai hoặc đi lại chút cho khoẻ nhé!", "");
                psyduck.Show();
            });
        }
    }
}
