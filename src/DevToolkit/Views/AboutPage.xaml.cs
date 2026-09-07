using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;

using DevToolkit.Services;

namespace DevToolkit.Views;

public partial class AboutPage : UserControl
{
    public AboutPage(PluginManager pluginManager)
    {
        InitializeComponent();
        LoadInfo();
        PluginList.ItemsSource = pluginManager.Plugins;
    }

    private void LoadInfo()
    {
        var asm = Assembly.GetExecutingAssembly();
        var version = asm.GetName().Version;
        VersionText.Text = version?.ToString(3) ?? "1.0.0";
        RuntimeText.Text = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription;
        OsText.Text = System.Runtime.InteropServices.RuntimeInformation.OSDescription;
        DotnetText.Text = Environment.Version.ToString();

        AppDirText.Text = AppServices.AppDir;
        DataDirText.Text = AppServices.DataRoot;
        LogsDirText.Text = AppServices.LogsDir;
        RecordingsDirText.Text = Path.Combine(AppServices.DataRoot, "recordings");
    }

    private static void OpenFolder(string path)
    {
        Directory.CreateDirectory(path);
        Process.Start(new ProcessStartInfo("explorer.exe", path) { UseShellExecute = true });
    }

    private void OpenAppDir_Click(object sender, RoutedEventArgs e) => OpenFolder(AppServices.AppDir);
    private void OpenDataDir_Click(object sender, RoutedEventArgs e) => OpenFolder(AppServices.DataRoot);
    private void OpenLogsDir_Click(object sender, RoutedEventArgs e) => OpenFolder(AppServices.LogsDir);
    private void OpenRecordingsDir_Click(object sender, RoutedEventArgs e) =>
        OpenFolder(Path.Combine(AppServices.DataRoot, "recordings"));
}
