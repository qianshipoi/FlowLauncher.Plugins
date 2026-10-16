using FlowLauncher.Plugin.S3.ViewModels;

using System.ComponentModel;
using System.Windows.Controls;

namespace FlowLauncher.Plugin.S3.Views;

public partial class S3SettingsView : UserControl
{
    private readonly S3SettingsViewModel _viewModel;

    public S3SettingsView(S3SettingsViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(S3SettingsViewModel.SelectedProfile))
            return;

        SecretBox.Password = "";
        TokenBox.Password = "";
    }

    private void SecretBox_PasswordChanged(object sender, System.Windows.RoutedEventArgs e)
    {
        _viewModel.AccessKeySecretInput = ((PasswordBox)sender).Password;
    }

    private void TokenBox_PasswordChanged(object sender, System.Windows.RoutedEventArgs e)
    {
        _viewModel.SessionTokenInput = ((PasswordBox)sender).Password;
    }
}
