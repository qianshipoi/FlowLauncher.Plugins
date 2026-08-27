using FlowLauncher.Plugin.PasswordManager.ViewModels;

using iNKORE.UI.WPF.Modern.Controls;

using System.Windows;
using System.Windows.Input;

namespace FlowLauncher.Plugin.PasswordManager.Views;

/// <summary>
/// 导入弹窗（支持拖拽或点击选择文件）
/// </summary>
public partial class ImportDialog : ContentDialog
{
    private readonly ImportViewModel _viewModel;

    internal ImportDialog(ImportViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;

        PrimaryButtonClick += OnPrimaryButtonClick;
    }

    private void DropZone_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private void DropZone_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(DataFormats.FileDrop) is string[] files && files.Length > 0)
            _viewModel.TryLoadFile(files[0]);
        e.Handled = true;
    }

    private void DropZone_Click(object sender, MouseButtonEventArgs e)
    {
        _viewModel.BrowseCommand.Execute(null);
    }

    private void OnPrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        if (!_viewModel.TryImport())
            args.Cancel = true;
    }
}