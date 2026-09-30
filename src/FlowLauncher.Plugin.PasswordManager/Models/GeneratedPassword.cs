namespace FlowLauncher.Plugin.PasswordManager.Models;

/// <summary>
/// 一次随机密码生成结果（作为结果的 ContextData，供右键菜单复用）。
/// </summary>
public sealed record GeneratedPassword(int Length, string Value, bool UserSpecified);
