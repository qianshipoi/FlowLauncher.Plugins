using DevToolkit.PasswordManager.ViewModels;

using iNKORE.UI.WPF.Modern.Controls;

namespace DevToolkit.Core.Wpf;

/// <summary>
/// 导出弹窗（选择格式和目标位置）
/// </summary>
public partial class ExportDialog : ContentDialog
{
    private readonly ExportViewModel _viewModel;

    internal ExportDialog(ExportViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;

        PrimaryButtonClick += OnPrimaryButtonClick;
    }

    private void OnPrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        if (!_viewModel.TryExport())
            args.Cancel = true;
    }
}