using System.Globalization;
using System.Windows;
using System.Windows.Controls;

namespace FlowLauncher.Plugin.PaperTodo.Views;

public partial class ReminderDialog : Window
{
    internal ReminderDialog(DateTimeOffset? current)
    {
        InitializeComponent();
        var initial = current?.ToLocalTime() ?? DateTimeOffset.Now.AddMinutes(15);
        ReminderDate.SelectedDate = initial.Date;
        ReminderTime.Text = initial.ToString("HH:mm", CultureInfo.InvariantCulture);
    }

    public DateTimeOffset? Result { get; private set; }

    private void PresetBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (CustomPanel is not null)
        {
            CustomPanel.Visibility = PresetBox.SelectedIndex == 5
                ? Visibility.Visible
                : Visibility.Collapsed;
        }
    }

    private void Clear_Click(object sender, RoutedEventArgs e)
    {
        Result = null;
        DialogResult = true;
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        var now = DateTimeOffset.Now;
        Result = PresetBox.SelectedIndex switch
        {
            0 => now.AddMinutes(15),
            1 => now.AddMinutes(30),
            2 => now.AddHours(1),
            3 => AtLocalTime(now.Date, new TimeSpan(18, 0, 0), ifPastAddDay: true),
            4 => new DateTimeOffset(now.Date.AddDays(1).AddHours(9), now.Offset),
            5 => ParseCustom(now),
            _ => now.AddMinutes(15),
        };

        if (Result is null)
        {
            return;
        }
        DialogResult = true;
    }

    private DateTimeOffset? ParseCustom(DateTimeOffset now)
    {
        if (ReminderDate.SelectedDate is not { } date ||
            !TimeSpan.TryParseExact(
                ReminderTime.Text.Trim(),
                [@"h\:mm", @"hh\:mm"],
                CultureInfo.InvariantCulture,
                out var time))
        {
            MessageBox.Show(this, "请输入有效的日期和 HH:mm 时间。", "设置提醒");
            return null;
        }

        return new DateTimeOffset(date.Date.Add(time), now.Offset);
    }

    private static DateTimeOffset AtLocalTime(DateTime date, TimeSpan time, bool ifPastAddDay)
    {
        var local = date.Add(time);
        if (ifPastAddDay && local <= DateTime.Now)
        {
            local = local.AddDays(1);
        }
        return new DateTimeOffset(local, TimeZoneInfo.Local.GetUtcOffset(local));
    }
}
