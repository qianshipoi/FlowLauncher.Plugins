using FlowLauncher.Plugin.PasswordManager.Services;
using FlowLauncher.Plugin.PasswordManager.ViewModels;

using iNKORE.UI.WPF.Modern.Controls;

using System.Windows;
using System.Windows.Input;

namespace FlowLauncher.Plugin.PasswordManager.Views;

/// <summary>
/// 密码数据管理窗口
/// </summary>
public partial class ManagerWindow : Window
{
    public ManagerWindow(PasswordStore store, ImportExportService importExport)
    {
        InitializeComponent();

        var viewModel = new ManagerViewModel(store, importExport);
        viewModel.EditRequested += vm => ShowDialog(new EditPasswordDialog(vm));
        viewModel.ImportRequested += vm => ShowDialog(new ImportDialog(vm));
        viewModel.ExportRequested += vm => ShowDialog(new ExportDialog(vm));
        DataContext = viewModel;
    }

    /// <summary>
    /// 视图层响应 VM 的请求：以 ContentDialog 展示弹窗
    /// </summary>
    private async void ShowDialog(ContentDialog dialog)
    {
        dialog.Owner = this;
        // 优先使用 iNKORE 预置样式（Flow Launcher 已加载主题资源）
        dialog.Style = Application.Current.Resources["DefaultContentDialogStyle"] as Style;
        await dialog.ShowAsync();
    }

    private void EntriesGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is ManagerViewModel vm && vm.SelectedEntry is not null)
            vm.EditCommand.Execute(null);
    }
}
