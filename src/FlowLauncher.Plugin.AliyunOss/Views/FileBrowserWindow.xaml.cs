using FlowLauncher.Plugin.AliyunOss.ViewModel;

using System.Windows;
using System.Windows.Input;

namespace FlowLauncher.Plugin.AliyunOss.Views;

public partial class FileBrowserWindow : Window
{
    private readonly FileBrowserViewModel _viewModel;
    private readonly string _bucketName;
    private readonly string _prefix;

    public FileBrowserWindow(FileBrowserViewModel viewModel, string bucketName, string prefix)
    {
        InitializeComponent();
        _viewModel = viewModel;
        _bucketName = bucketName;
        _prefix = prefix;
        DataContext = viewModel;
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _viewModel.Init(_bucketName, _prefix);
    }

    private void FileList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (_viewModel.SelectedFile is { IsDirectory: true } dir)
        {
            _viewModel.NavigateToFolder(dir.Key);
        }
    }

    private void BtnBack_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.NavigateUp();
    }

    private void DropZone_DragEnter(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            e.Effects = DragDropEffects.Copy;
            _viewModel.IsDragOver = true;
        }
        else
        {
            e.Effects = DragDropEffects.None;
        }
        e.Handled = true;
    }

    private void DropZone_DragLeave(object sender, DragEventArgs e)
    {
        _viewModel.IsDragOver = false;
        e.Handled = true;
    }

    private async void DropZone_Drop(object sender, DragEventArgs e)
    {
        _viewModel.IsDragOver = false;

        if (e.Data.GetDataPresent(DataFormats.FileDrop) &&
            e.Data.GetData(DataFormats.FileDrop) is string[] { Length: > 0 } files)
        {
            await _viewModel.HandleDropAsync(files);
        }

        e.Handled = true;
    }
}
