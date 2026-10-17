using FlowLauncher.Plugin.PaperTodo.Models;
using FlowLauncher.Plugin.PaperTodo.Services;
using FlowLauncher.Plugin.PaperTodo.ViewModels;

using Microsoft.Win32;

using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace FlowLauncher.Plugin.PaperTodo.Views;

public partial class TodoPaperWindow : Window
{
    private readonly PaperDatabase _database;
    private readonly PaperWindowManager _windowManager;
    private readonly TodoPaperViewModel _viewModel;
    private TodoItemViewModel? _dragItem;
    private Point _dragStart;
    private bool _allowClose;

    internal TodoPaperWindow(
        PaperDatabase database,
        Settings settings,
        Paper paper,
        PaperWindowManager windowManager)
    {
        InitializeComponent();
        _database = database;
        _windowManager = windowManager;
        _viewModel = new TodoPaperViewModel(database, settings, paper);
        _viewModel.ErrorOccurred += message =>
            MessageBox.Show(this, message, "PaperTodo", MessageBoxButton.OK, MessageBoxImage.Error);
        DataContext = _viewModel;
        UpdatePinButton();
        Loaded += async (_, _) => await _viewModel.LoadAsync();
        Closing += OnClosing;
    }

    public string PaperId => _viewModel.Paper.Id;

    public void FocusItem(string? itemId)
    {
        if (string.IsNullOrWhiteSpace(itemId))
        {
            return;
        }
        var item = _viewModel.Items.FirstOrDefault(candidate =>
            string.Equals(candidate.Id, itemId, StringComparison.Ordinal));
        if (item is null)
        {
            return;
        }
        TodoList.SelectedItem = item;
        TodoList.ScrollIntoView(item);
        Dispatcher.BeginInvoke(() =>
        {
            var container = TodoList.ItemContainerGenerator.ContainerFromItem(item) as ListBoxItem;
            container?.Focus();
        });
    }

    public void CloseWithoutPrompt()
    {
        _allowClose = true;
        Close();
    }

    public void RefreshTopmostButton() => UpdatePinButton();

    private async void OnClosing(object? sender, CancelEventArgs e)
    {
        if (_allowClose)
        {
            return;
        }
        try
        {
            if (!await _viewModel.CommitAllAsync())
            {
                e.Cancel = true;
                MessageBox.Show(
                    this,
                    "有待办内容保存失败，窗口将保持打开。请检查数据目录权限后重试。",
                    "PaperTodo",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
        }
        catch (Exception ex)
        {
            e.Cancel = true;
            MessageBox.Show(
                this,
                $"保存窗口标题失败：{ex.Message}",
                "PaperTodo",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private async void AddItem_Click(object sender, RoutedEventArgs e)
    {
        if (await _viewModel.AddNewAsync())
        {
            NewItemBox.Focus();
        }
    }

    private async void NewItemBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && Keyboard.Modifiers == ModifierKeys.Control)
        {
            e.Handled = true;
            await _viewModel.AddNewAsync();
        }
    }

    private async void DoneCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        if (sender is CheckBox { Tag: TodoItemViewModel item } checkBox)
        {
            await _viewModel.SetDoneAsync(item, checkBox.IsChecked == true);
        }
    }

    private async void ItemTextBox_LostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (sender is TextBox { DataContext: TodoItemViewModel item })
        {
            await _viewModel.CommitItemTextAsync(item);
        }
    }

    private async void TitleBox_LostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        await _viewModel.CommitTitleAsync();
    }

    private void PinButton_Click(object sender, RoutedEventArgs e)
    {
        Topmost = !Topmost;
        UpdatePinButton();
        _windowManager.SaveGeometrySoon(this, _viewModel.Paper.Id);
    }

    private void UpdatePinButton()
    {
        PinButton.Content = Topmost ? "已置顶" : "置顶";
        PinButton.Background = Topmost
            ? new SolidColorBrush(Color.FromRgb(0xDA, 0xE8, 0xF6))
            : new SolidColorBrush(Color.FromRgb(0xE9, 0xE5, 0xDC));
    }

    private void DragHandle_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement { Tag: TodoItemViewModel item })
        {
            _dragItem = item;
            _dragStart = e.GetPosition(TodoList);
        }
    }

    private void TodoList_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (_dragItem is null || e.LeftButton != MouseButtonState.Pressed)
        {
            return;
        }
        var position = e.GetPosition(TodoList);
        if (Math.Abs(position.Y - _dragStart.Y) < SystemParameters.MinimumVerticalDragDistance)
        {
            return;
        }

        DragDrop.DoDragDrop(TodoList, _dragItem.Id, DragDropEffects.Move);
        _dragItem = null;
    }

    private void TodoList_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.Text)
            ? DragDropEffects.Move
            : DragDropEffects.None;
        e.Handled = true;
    }

    private async void TodoList_Drop(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(DataFormats.Text) ||
            e.Data.GetData(DataFormats.Text) is not string itemId)
        {
            return;
        }

        var dragged = _viewModel.Items.FirstOrDefault(item =>
            string.Equals(item.Id, itemId, StringComparison.Ordinal));
        if (dragged is null)
        {
            return;
        }

        var targetContainer = ItemsControl.ContainerFromElement(
            TodoList,
            e.OriginalSource as DependencyObject) as ListBoxItem;
        var target = targetContainer?.DataContext as TodoItemViewModel;
        var oldIndex = _viewModel.Items.IndexOf(dragged);
        var newIndex = target is null
            ? _viewModel.Items.Count - 1
            : _viewModel.Items.IndexOf(target);
        if (target is not null &&
            targetContainer is not null &&
            e.GetPosition(targetContainer).Y > targetContainer.ActualHeight / 2)
        {
            newIndex++;
        }
        newIndex = Math.Clamp(newIndex, 0, _viewModel.Items.Count - 1);
        if (oldIndex == newIndex)
        {
            return;
        }

        _viewModel.Items.RemoveAt(oldIndex);
        if (oldIndex < newIndex)
        {
            newIndex--;
        }
        _viewModel.Items.Insert(Math.Clamp(newIndex, 0, _viewModel.Items.Count), dragged);
        await _viewModel.ReorderAsync(_viewModel.Items.ToArray());
    }

    private async void ItemMenuButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: TodoItemViewModel item } button)
        {
            return;
        }

        var menu = new ContextMenu();
        menu.Items.Add(BuildMenuItem(
            "打开关联",
            async () => await OpenLinkedAsync(item),
            !string.IsNullOrWhiteSpace(item.LinkedPaperId) || !string.IsNullOrWhiteSpace(item.LinkedPath)));
        menu.Items.Add(BuildMenuItem(
            "设置提醒",
            async () =>
            {
                var dialog = new ReminderDialog(item.ReminderAt) { Owner = this };
                await Task.CompletedTask;
                if (dialog.ShowDialog() == true)
                {
                    await _viewModel.SetReminderAsync(item, dialog.Result);
                }
            }));
        menu.Items.Add(new Separator());
        menu.Items.Add(BuildMenuItem("关联文件", async () => await LinkFileAsync(item)));
        menu.Items.Add(BuildMenuItem("关联文件夹", async () => await LinkFolderAsync(item)));
        menu.Items.Add(BuildMenuItem("关联笔记", async () => await LinkNoteAsync(item)));
        menu.Items.Add(BuildMenuItem("取消关联", async () => await _viewModel.ClearLinkAsync(item.Id)));
        menu.Items.Add(new Separator());
        menu.Items.Add(BuildMenuItem("删除事项", async () =>
        {
            if (MessageBox.Show(
                    this,
                    $"确定删除“{item.Text}”吗？",
                    "PaperTodo",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                await _viewModel.DeleteItemAsync(item);
            }
        }));

        menu.PlacementTarget = button;
        menu.IsOpen = true;
    }

    private static MenuItem BuildMenuItem(string header, Func<Task> action, bool enabled = true)
    {
        var item = new MenuItem { Header = header, IsEnabled = enabled };
        item.Click += async (_, _) => await action();
        return item;
    }

    private async Task OpenLinkedAsync(TodoItemViewModel item)
    {
        if (!string.IsNullOrWhiteSpace(item.LinkedPaperId))
        {
            await _windowManager.OpenPaperByIdAsync(item.LinkedPaperId);
            return;
        }
        if (string.IsNullOrWhiteSpace(item.LinkedPath))
        {
            return;
        }

        try
        {
            if (!File.Exists(item.LinkedPath) && !Directory.Exists(item.LinkedPath))
            {
                throw new FileNotFoundException("关联路径不存在。", item.LinkedPath);
            }
            Process.Start(new ProcessStartInfo(item.LinkedPath) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "无法打开关联", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private async Task LinkFileAsync(TodoItemViewModel item)
    {
        var dialog = new OpenFileDialog { Title = "关联文件" };
        if (dialog.ShowDialog(this) == true)
        {
            await _viewModel.SetFileLinkAsync(item.Id, dialog.FileName, isDirectory: false);
        }
    }

    private async Task LinkFolderAsync(TodoItemViewModel item)
    {
        var dialog = new OpenFolderDialog { Title = "关联文件夹" };
        if (dialog.ShowDialog(this) == true)
        {
            await _viewModel.SetFileLinkAsync(item.Id, dialog.FolderName, isDirectory: true);
        }
    }

    private async Task LinkNoteAsync(TodoItemViewModel item)
    {
        var notes = await _database.GetRecentNotesAsync(20);
        if (notes.Count == 0)
        {
            MessageBox.Show(
                this,
                "还没有可关联的笔记。",
                "PaperTodo",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        var menu = new ContextMenu();
        foreach (var note in notes)
        {
            var menuItem = new MenuItem { Header = note.Title };
            menuItem.Click += async (_, _) => await _viewModel.SetNoteLinkAsync(item.Id, note.Id);
            menu.Items.Add(menuItem);
        }
        menu.PlacementTarget = TodoList;
        menu.IsOpen = true;
    }
}
