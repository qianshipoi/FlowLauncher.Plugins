using DevToolkit.Abstractions;

namespace DevToolkit.Services;

/// <summary>
/// 编译期内置插件注册表：宿主以 ProjectReference 引用核心插件，并在此创建其实例完成编译期注册。
/// 各核心模块完成插件化改造后在 <see cref="Create"/> 中逐个注册。
/// </summary>
public static class BuiltInPlugins
{
    public static IEnumerable<IDevToolkitPlugin> Create()
    {
        yield return new DevToolkit.Toolbox.DevToolkitPlugin();
        yield return new DevToolkit.PortKiller.DevToolkitPlugin();
        yield return new DevToolkit.VarName.DevToolkitPlugin();
        yield return new DevToolkit.SpeechToText.DevToolkitPlugin();
        yield return new DevToolkit.PasswordManager.DevToolkitPlugin();
        yield return new DevToolkit.AliyunOss.DevToolkitPlugin();
        yield return new DevToolkit.OpenDirectory.DevToolkitPlugin();
    }
}
