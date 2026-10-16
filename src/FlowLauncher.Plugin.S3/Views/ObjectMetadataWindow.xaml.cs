using FlowLauncher.Plugin.S3.ViewModels;

using System.Windows;

namespace FlowLauncher.Plugin.S3.Views;

public partial class ObjectMetadataWindow : Window
{
    private readonly ObjectMetadataViewModel _viewModel;

    public ObjectMetadataWindow(ObjectMetadataViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;
        Loaded += async (_, _) => await _viewModel.LoadAsync();
    }

    private void RemoveSelected_Click(object sender, RoutedEventArgs e)
    {
        if (MetadataGrid.SelectedItem is MetadataEntryEditor entry)
            _viewModel.RemoveEntryCommand.Execute(entry);
    }
}
