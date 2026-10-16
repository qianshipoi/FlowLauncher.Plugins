using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using FlowLauncher.Plugin.S3.Services;
using FlowLauncher.Plugin.S3.Utils;

using System.Collections.ObjectModel;

namespace FlowLauncher.Plugin.S3.ViewModels;

public partial class S3ProfileEditor : ObservableObject
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    [ObservableProperty]
    private string _name = "Default";

    [ObservableProperty]
    private S3ProviderPreset _preset = S3ProviderPreset.Custom;

    [ObservableProperty]
    private string _endpoint = "";

    [ObservableProperty]
    private string _region = "us-east-1";

    [ObservableProperty]
    private S3AddressingStyle _addressingStyle = S3AddressingStyle.Path;

    [ObservableProperty]
    private string _accessKeyId = "";

    [ObservableProperty]
    private string _testBucket = "";

    [ObservableProperty]
    private string _aliyunNativeEndpoint = "";

    [ObservableProperty]
    private bool _disablePayloadSigning;

    [ObservableProperty]
    private bool _disableDefaultChecksumValidation;

    public string StoredAccessKeySecret { get; set; } = "";
    public string StoredSessionToken { get; set; } = "";

    public static S3ProfileEditor FromProfile(S3Profile profile)
    {
        return new S3ProfileEditor
        {
            Id = profile.Id,
            Name = profile.Name,
            Preset = profile.Preset,
            Endpoint = profile.Endpoint,
            Region = profile.Region,
            AddressingStyle = profile.AddressingStyle,
            AccessKeyId = profile.AccessKeyId,
            TestBucket = profile.TestBucket,
            AliyunNativeEndpoint = profile.AliyunNativeEndpoint,
            DisablePayloadSigning = profile.DisablePayloadSigning,
            DisableDefaultChecksumValidation = profile.DisableDefaultChecksumValidation,
            StoredAccessKeySecret = profile.AccessKeySecret,
            StoredSessionToken = profile.SessionToken
        };
    }

    public void ApplyTo(S3Profile profile)
    {
        profile.Id = Id;
        profile.Name = string.IsNullOrWhiteSpace(Name) ? "Default" : Name.Trim();
        profile.Preset = Preset;
        profile.Endpoint = S3ProviderPresets.NormalizeEndpoint(Endpoint);
        profile.Region = (Region ?? "").Trim();
        profile.AddressingStyle = AddressingStyle;
        profile.AccessKeyId = (AccessKeyId ?? "").Trim();
        profile.TestBucket = (TestBucket ?? "").Trim();
        profile.AliyunNativeEndpoint = S3ProviderPresets.NormalizeEndpoint(AliyunNativeEndpoint);
        profile.DisablePayloadSigning = DisablePayloadSigning;
        profile.DisableDefaultChecksumValidation = DisableDefaultChecksumValidation;
        profile.AccessKeySecret = StoredAccessKeySecret;
        profile.SessionToken = StoredSessionToken;
    }

    public S3Profile ToTemporaryProfile(string? secretInput, string? tokenInput, bool clearToken)
    {
        var profile = new S3Profile();
        ApplyTo(profile);
        profile.SetSecrets(AccessKeyId, secretInput, tokenInput, clearToken);
        return profile;
    }

    public void ApplyPreset(S3ProviderPreset preset)
    {
        Preset = preset;

        switch (preset)
        {
            case S3ProviderPreset.AwsS3:
                Endpoint = "";
                Region = string.IsNullOrWhiteSpace(Region) ? "us-east-1" : Region;
                AddressingStyle = S3AddressingStyle.VirtualHosted;
                DisablePayloadSigning = false;
                DisableDefaultChecksumValidation = false;
                break;
            case S3ProviderPreset.AliyunOss:
                Region = string.IsNullOrWhiteSpace(Region) ? "cn-hangzhou" : Region;
                Endpoint = $"https://s3.oss-{Region}.aliyuncs.com";
                AliyunNativeEndpoint = $"https://oss-{Region}.aliyuncs.com";
                AddressingStyle = S3AddressingStyle.VirtualHosted;
                DisablePayloadSigning = false;
                DisableDefaultChecksumValidation = true;
                break;
            case S3ProviderPreset.Minio:
                Region = string.IsNullOrWhiteSpace(Region) ? "us-east-1" : Region;
                AddressingStyle = S3AddressingStyle.Path;
                DisablePayloadSigning = false;
                DisableDefaultChecksumValidation = false;
                break;
            case S3ProviderPreset.Custom:
                break;
        }
    }

    partial void OnPresetChanged(S3ProviderPreset value) => ApplyPreset(value);
}

public partial class S3SettingsViewModel : ObservableObject
{
    private readonly Settings _settings;
    private readonly Action _onChanged;

    public S3SettingsViewModel(Settings settings, Action onChanged)
    {
        _settings = settings;
        _onChanged = onChanged;
        _settings.EnsureProfiles();

        Profiles = new ObservableCollection<S3ProfileEditor>(
            settings.Profiles.Select(S3ProfileEditor.FromProfile));
        SelectedProfile = Profiles.FirstOrDefault(profile =>
                            profile.Id == settings.ActiveProfileId)
                        ?? Profiles.FirstOrDefault();
        RefreshSecretHint();
    }

    public ObservableCollection<S3ProfileEditor> Profiles { get; }

    public IReadOnlyList<S3ProviderPreset> ProviderPresets { get; } =
        Enum.GetValues<S3ProviderPreset>();

    public IReadOnlyList<S3AddressingStyle> AddressingStyles { get; } =
        Enum.GetValues<S3AddressingStyle>();

    [ObservableProperty]
    private S3ProfileEditor? _selectedProfile;

    [ObservableProperty]
    private string _accessKeySecretInput = "";

    [ObservableProperty]
    private string _sessionTokenInput = "";

    [ObservableProperty]
    private bool _clearSessionToken;

    [ObservableProperty]
    private string _accessKeySecretHint = "";

    [ObservableProperty]
    private string _testStatus = "";

    [ObservableProperty]
    private bool _isTesting;

    public bool ComputeFileMd5
    {
        get => _settings.ComputeFileMd5;
        set
        {
            if (_settings.ComputeFileMd5 == value)
                return;
            _settings.ComputeFileMd5 = value;
            OnPropertyChanged();
        }
    }

    partial void OnSelectedProfileChanged(S3ProfileEditor? value)
    {
        ClearSessionToken = false;
        AccessKeySecretInput = "";
        SessionTokenInput = "";
        RefreshSecretHint();
    }

    private void RefreshSecretHint()
    {
        if (SelectedProfile is null)
        {
            AccessKeySecretHint = "";
            return;
        }

        AccessKeySecretHint = string.IsNullOrEmpty(SelectedProfile.StoredAccessKeySecret)
            ? "尚未配置"
            : "已配置，留空则不修改";
    }

    [RelayCommand]
    private void AddProfile()
    {
        var editor = new S3ProfileEditor();
        Profiles.Add(editor);
        SelectedProfile = editor;
        TestStatus = "已新增配置，保存后生效";
    }

    [RelayCommand]
    private void DuplicateProfile()
    {
        if (SelectedProfile is null)
            return;

        var editor = S3ProfileEditor.FromProfile(new S3Profile
        {
            Id = Guid.NewGuid().ToString("N"),
            Name = SelectedProfile.Name + " Copy",
            Preset = SelectedProfile.Preset,
            Endpoint = SelectedProfile.Endpoint,
            Region = SelectedProfile.Region,
            AddressingStyle = SelectedProfile.AddressingStyle,
            AccessKeyId = SelectedProfile.AccessKeyId,
            TestBucket = SelectedProfile.TestBucket,
            AliyunNativeEndpoint = SelectedProfile.AliyunNativeEndpoint,
            DisablePayloadSigning = SelectedProfile.DisablePayloadSigning,
            DisableDefaultChecksumValidation = SelectedProfile.DisableDefaultChecksumValidation,
            AccessKeySecret = SelectedProfile.StoredAccessKeySecret,
            SessionToken = SelectedProfile.StoredSessionToken
        });

        Profiles.Add(editor);
        SelectedProfile = editor;
        TestStatus = "已复制配置，保存后生效";
    }

    [RelayCommand]
    private void DeleteProfile()
    {
        if (SelectedProfile is null)
            return;

        if (Profiles.Count == 1)
        {
            DeleteProfileCore(SelectedProfile);
            Profiles.Add(new S3ProfileEditor());
            SelectedProfile = Profiles[0];
            TestStatus = "已重置配置";
            return;
        }

        var index = Profiles.IndexOf(SelectedProfile);
        Profiles.Remove(SelectedProfile);
        SelectedProfile = Profiles[Math.Clamp(index, 0, Profiles.Count - 1)];
        TestStatus = "已删除配置，保存后生效";
    }

    private void DeleteProfileCore(S3ProfileEditor editor)
    {
        editor.Name = "Deleted";
        editor.StoredAccessKeySecret = "";
        editor.StoredSessionToken = "";
        editor.AccessKeyId = "";
        Profiles.Remove(editor);
    }

    public void ApplySelectedPreset()
    {
        SelectedProfile?.ApplyPreset(SelectedProfile.Preset);
    }

    [RelayCommand]
    private void Save()
    {
        if (SelectedProfile is null)
            return;

        var editorIds = Profiles.Select(profile => profile.Id).ToHashSet(StringComparer.Ordinal);
        _settings.Profiles.RemoveAll(profile => !editorIds.Contains(profile.Id));

        foreach (var editor in Profiles)
        {
            var profileTarget = _settings.Profiles.FirstOrDefault(profile => profile.Id == editor.Id);
            if (profileTarget is null)
            {
                profileTarget = new S3Profile { Id = editor.Id };
                _settings.Profiles.Add(profileTarget);
            }

            editor.ApplyTo(profileTarget);
        }

        var target = _settings.Profiles.First(profile => profile.Id == SelectedProfile.Id);
        SelectedProfile.ApplyTo(target);
        var secret = string.IsNullOrEmpty(AccessKeySecretInput) ? null : AccessKeySecretInput;
        var token = string.IsNullOrEmpty(SessionTokenInput) ? null : SessionTokenInput;
        target.SetSecrets(SelectedProfile.AccessKeyId, secret, token, ClearSessionToken);
        SelectedProfile.StoredAccessKeySecret = target.AccessKeySecret;
        SelectedProfile.StoredSessionToken = target.SessionToken;

        _settings.ActiveProfileId = target.Id;
        _settings.EnsureProfiles();
        _onChanged();

        AccessKeySecretInput = "";
        SessionTokenInput = "";
        ClearSessionToken = false;
        RefreshSecretHint();
        TestStatus = "已保存";
    }

    [RelayCommand]
    private async Task TestAsync()
    {
        if (IsTesting || SelectedProfile is null)
            return;

        var secret = string.IsNullOrEmpty(AccessKeySecretInput)
            ? null
            : AccessKeySecretInput;
        var token = string.IsNullOrEmpty(SessionTokenInput)
            ? null
            : SessionTokenInput;
        var profile = SelectedProfile.ToTemporaryProfile(secret, token, ClearSessionToken);

        IsTesting = true;
        TestStatus = "正在连接…";

        try
        {
            var settings = new Settings
            {
                Profiles = [profile],
                ActiveProfileId = profile.Id
            };
            var service = new S3StorageService(settings);

            if (string.IsNullOrWhiteSpace(profile.TestBucket))
            {
                var buckets = await service.GetBucketsAsync();
                TestStatus = $"连接成功，共 {buckets.Count} 个 Bucket";
            }
            else
            {
                await service.GetObjectsAsync(profile.TestBucket, "");
                TestStatus = $"连接成功，Buckets: {profile.TestBucket}";
            }
        }
        catch (Exception ex)
        {
            TestStatus = $"连接失败：{ex.Message}";
        }
        finally
        {
            IsTesting = false;
        }
    }
}
