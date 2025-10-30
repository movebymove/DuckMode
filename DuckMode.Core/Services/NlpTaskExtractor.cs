using System.Globalization;
using System.Text.RegularExpressions;
using DuckMode.Core.Contracts;

namespace DuckMode.Core.Services;

public class SimpleViNlpTaskExtractor : INlpTaskExtractor
{
    private static readonly Regex TimeRegex = new("\\b(?<h>\\d{1,2})(?:h|:(?<m>\\d{2}))?\\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex RelativeMinutesRegex = new("\\b(sau|trong)\\s+(?<n>\\d{1,3})\\s*(phut|phút|minute|minutes|mins)\\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex RelativeHoursRegex = new("\\b(sau|trong)\\s+(?<n>\\d{1,2})\\s*(gio|giờ|hour|hours|h)\\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public ExtractResult Extract(string naturalText, DateTime now, CultureInfo culture)
    {
        var text = naturalText.Trim();
        var due = ParseDateTime(text, now);

        var title = CleanTitle(text);

        if (due is null)
        {
            return new ExtractResult(false, new TaskItem { Title = title }, "due");
        }

        // Calculate appropriate reminder time based on deadline proximity
        var timeUntilDeadline = due.Value - now;
        int remindBeforeMinutes;
        
        if (timeUntilDeadline.TotalMinutes <= 0)
        {
            // Deadline is in the past, remind immediately
            remindBeforeMinutes = 0;
        }
        else if (timeUntilDeadline.TotalMinutes <= 5)
        {
            // Very close deadline (≤5 min), remind immediately
            remindBeforeMinutes = 0;
        }
        else if (timeUntilDeadline.TotalMinutes <= 30)
        {
            // Close deadline (5-30 min), remind 5 minutes before
            remindBeforeMinutes = Math.Max(1, (int)timeUntilDeadline.TotalMinutes - 1);
        }
        else if (timeUntilDeadline.TotalMinutes <= 60)
        {
            // Near deadline (30-60 min), remind 10 minutes before
            remindBeforeMinutes = 10;
        }
        else if (timeUntilDeadline.TotalHours <= 4)
        {
            // Medium deadline (1-4 hours), remind 30 minutes before
            remindBeforeMinutes = 30;
        }
        else
        {
            // Long deadline (>4 hours), remind 1 hour before
            remindBeforeMinutes = 60;
        }

        return new ExtractResult(true, new TaskItem { Title = title, DueAt = due, RemindBeforeMinutes = remindBeforeMinutes }, null);
    }

    private static string CleanTitle(string raw)
    {
        var title = raw;

        // Strip command prefix and common lead-in phrases
        title = Regex.Replace(title, "^/r\\s*", "", RegexOptions.IgnoreCase);
        title = Regex.Replace(title, "^(à,|a,|hãy|vui lòng|xin|làm ơn|xem nào|xem nao|ờ|ừ|uhm|ờm|ơ|ồ)\\s*", "", RegexOptions.IgnoreCase);
        title = Regex.Replace(title, "^(nhắc\\s+(tôi|toi|mình|minh))\\s*", "", RegexOptions.IgnoreCase);
        
        // Remove common pronouns and auxiliaries
        title = Regex.Replace(title, "\\b(tôi|toi|mình|minh|tớ|to|tao|em|anh|chị|ban|bạn|mày|ta|chúng tôi|chung toi)\\b", "", RegexOptions.IgnoreCase);
        title = Regex.Replace(title, "\\b(phải|cần|sẽ|đang|vừa|se|dang|vua)\\b", "", RegexOptions.IgnoreCase);

        // Remove time expressions
        title = Regex.Replace(title, "\\b\\d{1,2}:\\d{2}\\b", "", RegexOptions.IgnoreCase); // 10:30
        title = Regex.Replace(title, "\\b\\d{1,2}h(\\d{2})?\\b", "", RegexOptions.IgnoreCase); // 10h30, 10h
        title = Regex.Replace(title, "\\b(sau|trong)\\s+\\d{1,3}\\s*(phut|phút|minute|minutes|mins|gio|giờ|hour|hours|h)\\b", "", RegexOptions.IgnoreCase);

        // Remove date/time words
        title = Regex.Replace(title, "\\b(hôm nay|hom nay|mai|ngày mai|chieu|chiều|sang|sáng|toi|tối|am|pm|lúc|luc|vao|vào|nay)\\b", "", RegexOptions.IgnoreCase);

        // Remove ending polite particles
        title = Regex.Replace(title, "(nhé|nha|với|voi|giúp|giup|giùm|gium)\\s*$", "", RegexOptions.IgnoreCase);

        // Collapse extra spaces and punctuation
        title = Regex.Replace(title, @"[\t ]+", " ");
        title = Regex.Replace(title, ",+", ", ");
        title = Regex.Replace(title, "\u00A0", " "); // non-breaking spaces
        title = title.Trim(' ', ',', '.', '!', '?', ':', ';');

        if (string.IsNullOrWhiteSpace(title))
            title = raw.Trim();

        return title;
    }

    private static DateTime? ParseDateTime(string text, DateTime now)
    {
        var lower = text.ToLowerInvariant();
        DateTime date = now.Date;

        // relative minutes/hours like "sau 5 phút", "trong 2 giờ"
        var mRelMin = RelativeMinutesRegex.Match(lower);
        if (mRelMin.Success)
        {
            var n = int.Parse(mRelMin.Groups["n"].Value);
            return now.AddMinutes(Math.Max(1, n));
        }
        var mRelHr = RelativeHoursRegex.Match(lower);
        if (mRelHr.Success)
        {
            var n = int.Parse(mRelHr.Groups["n"].Value);
            return now.AddHours(Math.Max(1, n));
        }

        if (lower.Contains("mai") || lower.Contains("ngày mai"))
            date = now.Date.AddDays(1);
        else if (lower.Contains("hôm nay") || lower.Contains("hom nay"))
            date = now.Date;

        int hour = -1;
        int minute = 0;
        var m = TimeRegex.Match(lower);
        if (m.Success)
        {
            hour = int.Parse(m.Groups["h"].Value);
            if (m.Groups["m"].Success)
                minute = int.Parse(m.Groups["m"].Value);

            // heuristics for "chiều" as PM
            if (lower.Contains("chiều") && hour >= 1 && hour <= 11) hour += 12;
        }

        if (hour >= 0 && hour <= 23)
        {
            return new DateTime(date.Year, date.Month, date.Day, hour, minute, 0);
        }

        return null;
    }
}



