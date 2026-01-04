using System.Windows.Controls;

namespace FlowLauncher.Plugin.Translator
{
    /// <summary>
    /// SettingsView.xaml 的交互逻辑
    /// </summary>
    public partial class SettingsView : UserControl
    {
        public SettingsView()
        {
            InitializeComponent();
        }

        public SettingsView(SettingsViewModel viewModel) : this()
        {
            DataContext = viewModel;
        }
    }
}
