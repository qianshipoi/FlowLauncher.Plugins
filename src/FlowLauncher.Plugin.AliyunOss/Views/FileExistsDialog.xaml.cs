using System.Windows;

namespace FlowLauncher.Plugin.AliyunOss.Views;

public partial class FileExistsDialog : Window
{
    public enum Choice { Cancel, Replace, BackupAndReplace }

    public Choice Result { get; private set; } = Choice.Cancel;

    public FileExistsDialog(string objectName)
    {
        InitializeComponent();
        MessageText.Text = $"对象 {objectName} 已存在，请选择操作：";
    }

    private void BtnBackupReplace_Click(object sender, RoutedEventArgs e)
    {
        Result = Choice.BackupAndReplace;
        DialogResult = true;
    }

    private void BtnReplace_Click(object sender, RoutedEventArgs e)
    {
        Result = Choice.Replace;
        DialogResult = true;
    }

    private void BtnCancel_Click(object sender, RoutedEventArgs e)
    {
        Result = Choice.Cancel;
        DialogResult = false;
        Close();
    }
}
