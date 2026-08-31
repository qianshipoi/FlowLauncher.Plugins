using System.Windows;
using System.Windows.Controls;
using DevToolkit.SpeechToText;

namespace DevToolkit.Views;

public partial class SpeechToTextPage : UserControl
{
    private readonly SpeechToTextViewModel _viewModel;

    public SpeechToTextPage(SpeechToTextViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;
    }

    private void RecordButton_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.Toggle();
    }

    private void CopyResult_Click(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrEmpty(_viewModel.ResultText))
            Clipboard.SetText(_viewModel.ResultText);
    }

    private void ClearResult_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.ResultText = "";
        _viewModel.HasResult = false;
    }

    private void History_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is ListBox listBox && listBox.SelectedItem is TranscriptionRecord record)
        {
            _viewModel.ResultText = record.Text;
            _viewModel.HasResult = true;
            _viewModel.StatusText = $"加载历史记录（{record.Timestamp:yyyy-MM-dd HH:mm}）";
        }
    }
}
