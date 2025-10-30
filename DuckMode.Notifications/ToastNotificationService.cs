using DuckMode.Core.Contracts;
using Microsoft.Toolkit.Uwp.Notifications;
using System.Diagnostics;

namespace DuckMode.Notifications;

public class ToastNotificationService : INotificationService
{
    public void ShowDeadline(TaskItem task)
    {
        Debug.WriteLine(string.IsNullOrWhiteSpace(task.Title) ? "Sắp đến deadline!" : $"Sắp đến deadline: {task.Title}");
        // (Muốn toast thật thì code ToastNotificationBuilder tại đây)
    }

    public WaterBreakAction ShowWaterBreak()
    {
        var result = System.Windows.Forms.MessageBox.Show(
            "Đến giờ uống nước/duỗi vai! Nghỉ 1-2 phút nhé. Snooze 15 phút?",
            "DuckMode",
            System.Windows.Forms.MessageBoxButtons.YesNo,
            System.Windows.Forms.MessageBoxIcon.Information);
        return result == System.Windows.Forms.DialogResult.Yes ? WaterBreakAction.Snooze15 : WaterBreakAction.Dismiss;
    }

    public void ShowMoveBreak()
    {
        System.Windows.Forms.MessageBox.Show(
            "Đến giờ vận động nhẹ! Đứng lên đi lại, duỗi cơ một chút nhé.",
            "DuckMode",
            System.Windows.Forms.MessageBoxButtons.OK,
            System.Windows.Forms.MessageBoxIcon.Information);
    }
}



