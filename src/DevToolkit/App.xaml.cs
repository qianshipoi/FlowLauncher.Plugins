using System.IO;

using System.Text.Json;

using System.Windows;

using DevToolkit.Toolbox;

namespace DevToolkit;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        // SQLCipher 原生库初始化（密码管理器依赖）
        SQLitePCL.Batteries_V2.Init();
        base.OnStartup(e);
    }
}
