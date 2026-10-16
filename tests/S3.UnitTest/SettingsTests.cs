using FlowLauncher.Plugin.S3;
using FlowLauncher.Plugin.S3.Utils;
using FlowLauncher.Plugin.S3.ViewModels;

namespace S3.UnitTest;

public class SettingsTests
{
    [Fact]
    public void EnsureProfilesCreatesAndSelectsDefaultProfile()
    {
        var settings = new Settings();

        settings.EnsureProfiles();

        var profile = Assert.Single(settings.Profiles);
        Assert.Equal(profile.Id, settings.ActiveProfileId);
        Assert.Equal("Default", profile.Name);
    }

    [Fact]
    public void ProfileSecretsRoundTripThroughDpapiAndKeepBlankValues()
    {
        var profile = new S3Profile();
        profile.SetSecrets("access-id", "secret-value", "token-value", clearSessionToken: false);

        Assert.True(SecretProtector.IsProtected(profile.AccessKeySecret));
        Assert.True(SecretProtector.IsProtected(profile.SessionToken));
        Assert.Equal("secret-value", profile.GetPlainAccessKeySecret());
        Assert.Equal("token-value", profile.GetPlainSessionToken());

        profile.SetSecrets("next-id", null, null, clearSessionToken: false);

        Assert.Equal("next-id", profile.AccessKeyId);
        Assert.Equal("secret-value", profile.GetPlainAccessKeySecret());
        Assert.Equal("token-value", profile.GetPlainSessionToken());
    }

    [Fact]
    public void SettingsViewModelDeletesAndAddsProfilesOnSave()
    {
        var settings = new Settings();
        settings.EnsureProfiles();
        var originalId = settings.Profiles[0].Id;
        var saved = false;
        var viewModel = new S3SettingsViewModel(settings, () => saved = true);

        viewModel.AddProfileCommand.Execute(null);
        viewModel.SelectedProfile!.Name = "Second";
        viewModel.SaveCommand.Execute(null);

        Assert.True(saved);
        Assert.Equal(2, settings.Profiles.Count);
        Assert.Contains(settings.Profiles, profile => profile.Name == "Second");

        viewModel.SelectedProfile = viewModel.Profiles.Single(profile => profile.Id == originalId);
        viewModel.DeleteProfileCommand.Execute(null);
        viewModel.SaveCommand.Execute(null);

        Assert.Single(settings.Profiles);
        Assert.Equal("Second", settings.Profiles[0].Name);
    }
}
