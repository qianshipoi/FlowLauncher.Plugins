using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Shapes;
using DevToolkit.AiChat;
using DevToolkit.Core.Wpf;

namespace DemoApp
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly Settings _settings;

        public MainWindow()
        {
            InitializeComponent();
            _settings = new Settings();
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            var window = new AiChatWindow(_settings, () => 
            {
                // Simulate saving settings
                System.Diagnostics.Debug.WriteLine($"Settings saved: Width={_settings.WindowWidth}, Height={_settings.WindowHeight}");
            });
            window.Show();
        }
    }
}