using CommunityToolkit.Mvvm.ComponentModel;

using FlowLauncher.Plugin.PasswordManager.Models;
using FlowLauncher.Plugin.PasswordManager.Services;

using System;

namespace FlowLauncher.Plugin.PasswordManager.ViewModels;

/// <summary>
/// 密码条目编辑 ViewModel（添加/编辑共用）
/// </summary>
public partial class EditPasswordViewModel : ObservableObject
{
    private readonly PasswordStore _store;
    private readonly PasswordEntry? _existing;

    /// <summary>保存成功后触发，参数为条目 ID</summary>
    public event Action<string>? Saved;

    [ObservableProperty]
    private string _title = "";

    [ObservableProperty]
    private string _username = "";

    /// <summary>明文密码（由视图从密码控件同步进来）</summary>
    [ObservableProperty]
    private string _password = "";

    [ObservableProperty]
    private string _url = "";

    [ObservableProperty]
    private string _tags = "";

    [ObservableProperty]
    private string _notes = "";

    /// <summary>是否明文显示密码（视图据此切换密码控件可见性）</summary>
    [ObservableProperty]
    private bool _showPassword;

    /// <summary>保存成功后返回的条目 ID</summary>
    public string CreatedId { get; private set; } = "";

    /// <summary>窗口标题（视图绑定）</summary>
    public string WindowTitle { get; }

    public EditPasswordViewModel(PasswordStore store, PasswordEntry? existing)
    {
        _store = store;
        _existing = existing;
        WindowTitle = existing is null ? "添加密码条目" : $"编辑: {existing.Title}";

        if (existing is not null)
        {
            Title = existing.Title;
            Username = existing.Username;
            // 仅在用户主动打开编辑窗口时解密显示
            Password = PasswordCrypto.Decrypt(existing.EncryptedPassword);
            Url = existing.Url;
            Tags = existing.Tags;
            Notes = existing.Notes;
        }
    }

    public bool Validate()
    {
        if (string.IsNullOrWhiteSpace(Title))
            return false;
        if (string.IsNullOrWhiteSpace(Username))
            return false;
        if (string.IsNullOrEmpty(Password))
            return false;
        return true;
    }

    /// <summary>
    /// 校验并保存，返回是否成功（ContentDialog 据此决定是否关闭）
    /// </summary>
    public bool TrySave()
    {
        if (string.IsNullOrWhiteSpace(Title))
        {
            System.Windows.MessageBox.Show("请输入标题", "校验失败", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
            return false;
        }
        if (string.IsNullOrWhiteSpace(Username))
        {
            System.Windows.MessageBox.Show("请输入用户名", "校验失败", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
            return false;
        }
        if (string.IsNullOrEmpty(Password))
        {
            System.Windows.MessageBox.Show("请输入密码", "校验失败", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
            return false;
        }

        if (_existing is null)
        {
            var entry = new PasswordEntry
            {
                Title = Title.Trim(),
                Username = Username.Trim(),
                EncryptedPassword = PasswordCrypto.Encrypt(Password),
                Url = Url.Trim(),
                Tags = Tags.Trim(),
                Notes = Notes,
            };
            _store.Add(entry);
            CreatedId = entry.Id;
        }
        else
        {
            _store.Update(new PasswordEntry
            {
                Id = _existing.Id,
                Title = Title.Trim(),
                Username = Username.Trim(),
                EncryptedPassword = PasswordCrypto.Encrypt(Password),
                Url = Url.Trim(),
                Tags = Tags.Trim(),
                Notes = Notes,
                CreatedAt = _existing.CreatedAt,
                UpdatedAt = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss"),
            });
            CreatedId = _existing.Id;
        }

        Saved?.Invoke(CreatedId);
        return true;
    }
}