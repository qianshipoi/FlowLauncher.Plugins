using DevToolkit.ViewModels;

using System.Windows.Controls;

namespace DevToolkit.Views;

public partial class OssPage : Page
{
    public OssPage(OssPageViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
