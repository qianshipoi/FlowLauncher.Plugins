using DevToolkit.PasswordManager.ViewModels;

using iNKORE.UI.WPF.Modern.Controls;

using System.Windows;
using System.Windows.Controls;

namespace DevToolkit.PasswordManager.Views;

/// <summary>
/// 密码条目编辑弹窗（ContentDialog，Win11 样式）
/// </summary>
public partial class EditPasswordDialog : ContentDialog
{
    private readonly EditPasswordViewModel _viewModel;

    internal EditPasswordDialog(EditPasswordViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;

        // 视图层同步：将已有密码填入密码控件
        if (!string.IsNullOrEmpty(viewModel.Password))
            PasswordInput.Password = viewModel.Password;

        // 响应 ShowPassword 变化，切换明文/遮蔽显示
        viewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(EditPasswordViewModel.ShowPassword))
                UpdatePasswordVisibility(viewModel.ShowPassword);
        };
        UpdatePasswordVisibility(viewModel.ShowPassword);

        PrimaryButtonClick += OnPrimaryButtonClick;
    }

    private void OnPrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        // 先同步密码控件值到 VM，再尝试保存
        _viewModel.Password = GetPasswordValue();
        if (!_viewModel.TrySave())
            args.Cancel = true; // 校验失败，保持弹窗打开
    }

    private string GetPasswordValue()
    {
        return PasswordVisible.Visibility == Visibility.Visible
            ? PasswordVisible.Text
            : PasswordInput.Password;
    }

    private void UpdatePasswordVisibility(bool show)
    {
        if (show)
        {
            PasswordVisible.Text = GetPasswordValue();
            PasswordVisible.Visibility = Visibility.Visible;
            PasswordInput.Visibility = Visibility.Collapsed;
            PasswordVisible.Focus();
            PasswordVisible.CaretIndex = PasswordVisible.Text.Length;
        }
        else
        {
            var current = PasswordVisible.Visibility == Visibility.Visible
                ? PasswordVisible.Text
                : PasswordInput.Password;
            PasswordInput.Password = current;
            PasswordVisible.Visibility = Visibility.Collapsed;
            PasswordInput.Visibility = Visibility.Visible;
        }
    }

    private void PasswordInput_PasswordChanged(object sender, RoutedEventArgs e)
    {
        // PasswordBox.Password 不可绑定，手动同步到 VM
        if (PasswordInput.Visibility == Visibility.Visible)
            _viewModel.Password = PasswordInput.Password;
    }

    private void PasswordVisible_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (PasswordVisible.Visibility == Visibility.Visible)
            _viewModel.Password = PasswordVisible.Text;
    }
}
