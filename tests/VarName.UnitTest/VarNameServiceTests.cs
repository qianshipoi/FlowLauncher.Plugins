using FlowLauncher.Plugin.VarName;

using Microsoft.Extensions.Configuration;

namespace VarName.UnitTest;

public class VarNameServiceTests
{
    #region ParseResponse 单元测试

    [Fact]
    public void ParseResponse_每行一个结果()
    {
        var input = "UserManagement\nUserAdmin\nAccountManagement";

        var result = VarNameService.ParseResponse(input);

        Assert.Equal(3, result.Count);
        Assert.Equal("UserManagement", result[0]);
        Assert.Equal("UserAdmin", result[1]);
        Assert.Equal("AccountManagement", result[2]);
    }

    [Fact]
    public void ParseResponse_过滤空行()
    {
        var input = "UserManagement\n\n\nUserAdmin\n";

        var result = VarNameService.ParseResponse(input);

        Assert.Equal(2, result.Count);
    }

    [Fact]
    public void ParseResponse_去除编号前缀()
    {
        var input = "1. UserManagement\n2) UserAdmin\n* AccountManagement\n• UserControl";

        var result = VarNameService.ParseResponse(input);

        Assert.Equal(4, result.Count);
        Assert.Equal("UserManagement", result[0]);
        Assert.Equal("UserAdmin", result[1]);
        Assert.Equal("AccountManagement", result[2]);
        Assert.Equal("UserControl", result[3]);
    }

    [Fact]
    public void ParseResponse_去除破折号前缀()
    {
        var input = "- UserManagement\n- UserAdmin";

        var result = VarNameService.ParseResponse(input);

        Assert.Equal(2, result.Count);
        Assert.Equal("UserManagement", result[0]);
        Assert.Equal("UserAdmin", result[1]);
    }

    [Fact]
    public void ParseResponse_去重()
    {
        var input = "UserManagement\nUserAdmin\nUserManagement";

        var result = VarNameService.ParseResponse(input);

        Assert.Equal(2, result.Count);
    }

    [Fact]
    public void ParseResponse_过滤含符号的无效行()
    {
        var input = "UserManagement\n// comment\nUserAdmin\n{invalid}";

        var result = VarNameService.ParseResponse(input);

        Assert.Equal(2, result.Count);
        Assert.Equal("UserManagement", result[0]);
        Assert.Equal("UserAdmin", result[1]);
    }

    [Fact]
    public void ParseResponse_空字符串返回空列表()
    {
        var result = VarNameService.ParseResponse("");

        Assert.Empty(result);
    }

    [Fact]
    public void ParseResponse_合并空格()
    {
        var input = "User Management\nAccount Admin";

        var result = VarNameService.ParseResponse(input);

        Assert.Equal(2, result.Count);
        Assert.Equal("UserManagement", result[0]);
        Assert.Equal("AccountAdmin", result[1]);
    }

    [Fact]
    public void ParseResponse_保留下划线()
    {
        var input = "user_management\nget_user_info\nlistOrder";

        var result = VarNameService.ParseResponse(input);

        Assert.Equal(3, result.Count);
        Assert.Equal("user_management", result[0]);
        Assert.Equal("get_user_info", result[1]);
        Assert.Equal("listOrder", result[2]);
    }

    #endregion

    #region GenerateAsync 集成测试（需要 API 配置）

    private static VarNameService? CreateService()
    {
        var configuration = new ConfigurationBuilder()
            .AddUserSecrets<VarNameServiceTests>()
            .Build();

        var apiKey = configuration["ApiKey"];
        var apiUrl = configuration["ApiUrl"];
        var modelName = configuration["ModelName"];

        if (string.IsNullOrWhiteSpace(apiKey))
            return null;

        return new VarNameService(new Settings
        {
            ApiKey = apiKey,
            ApiUrl = apiUrl ?? "https://api.openai.com/v1/chat/completions",
            ModelName = modelName ?? "gpt-3.5-turbo",
        });
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task GenerateAsync_中文输入返回结果()
    {
        var service = CreateService();
        if (service == null)
        {
            // 未配置 API Key，跳过
            return;
        }

        var result = await service.GenerateAsync("用户管理", CancellationToken.None);

        Assert.NotNull(result);
        Assert.NotEmpty(result);
        Assert.All(result, name => Assert.False(string.IsNullOrWhiteSpace(name)));
    }

    #endregion
}
