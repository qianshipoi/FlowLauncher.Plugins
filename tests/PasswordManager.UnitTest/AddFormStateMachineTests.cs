using DevToolkit.PasswordManager.Services;

using Xunit;

namespace FlowLauncher.Plugin.PasswordManager.Tests;

public class AddFormStateMachineTests
{
    [Fact]
    public void InitialState_IsNone()
    {
        var form = new AddFormStateMachine();
        Assert.Equal(AddFormStateMachine.Step.None, form.CurrentStep);
        Assert.False(form.IsActive);
    }

    [Fact]
    public void Start_TransitionsToTitle()
    {
        var form = new AddFormStateMachine();
        form.Start();
        Assert.Equal(AddFormStateMachine.Step.Title, form.CurrentStep);
        Assert.True(form.IsActive);
    }

    [Fact]
    public void Submit_Title_AdvancesToUsername()
    {
        var form = new AddFormStateMachine();
        form.Start();

        var result = form.Submit("GitHub");

        Assert.True(result);
        Assert.Equal(AddFormStateMachine.Step.Username, form.CurrentStep);
        Assert.Equal("GitHub", form.Title);
    }

    [Fact]
    public void Submit_Username_AdvancesToPassword()
    {
        var form = new AddFormStateMachine();
        form.Start();
        form.Submit("GitHub");

        var result = form.Submit("admin@test.com");

        Assert.True(result);
        Assert.Equal(AddFormStateMachine.Step.Password, form.CurrentStep);
        Assert.Equal("admin@test.com", form.Username);
    }

    [Fact]
    public void Submit_Password_AdvancesToConfirm()
    {
        var form = new AddFormStateMachine();
        form.Start();
        form.Submit("GitHub");
        form.Submit("admin@test.com");

        var result = form.Submit("P@ssw0rd");

        Assert.True(result);
        Assert.Equal(AddFormStateMachine.Step.Confirm, form.CurrentStep);
        Assert.Equal("P@ssw0rd", form.Password);
    }

    [Fact]
    public void Submit_EmptyValue_ReturnsFalse()
    {
        var form = new AddFormStateMachine();
        form.Start();

        Assert.False(form.Submit(""));
        Assert.False(form.Submit("   "));
        Assert.False(form.Submit(null!));

        // 状态不变
        Assert.Equal(AddFormStateMachine.Step.Title, form.CurrentStep);
    }

    [Fact]
    public void Submit_TrimsWhitespace()
    {
        var form = new AddFormStateMachine();
        form.Start();

        form.Submit("  GitHub  ");
        Assert.Equal("GitHub", form.Title);
    }

    [Fact]
    public void Submit_ConfirmStep_ReturnsFalse()
    {
        var form = new AddFormStateMachine();
        form.Start();
        form.Submit("GitHub");
        form.Submit("admin");
        form.Submit("pass");

        Assert.False(form.Submit("anything"));
    }

    [Fact]
    public void GoBack_FromUsername_ToTitle()
    {
        var form = new AddFormStateMachine();
        form.Start();
        form.Submit("GitHub");

        form.GoBack();

        Assert.Equal(AddFormStateMachine.Step.Title, form.CurrentStep);
        Assert.Equal("GitHub", form.Title); // 保留已填数据
    }

    [Fact]
    public void GoBack_FromPassword_ToUsername()
    {
        var form = new AddFormStateMachine();
        form.Start();
        form.Submit("GitHub");
        form.Submit("admin");

        form.GoBack();

        Assert.Equal(AddFormStateMachine.Step.Username, form.CurrentStep);
    }

    [Fact]
    public void GoBack_FromConfirm_ToPassword()
    {
        var form = new AddFormStateMachine();
        form.Start();
        form.Submit("GitHub");
        form.Submit("admin");
        form.Submit("pass");

        form.GoBack();

        Assert.Equal(AddFormStateMachine.Step.Password, form.CurrentStep);
    }

    [Fact]
    public void GoBack_FromTitle_DoesNothing()
    {
        var form = new AddFormStateMachine();
        form.Start();

        form.GoBack();

        Assert.Equal(AddFormStateMachine.Step.Title, form.CurrentStep);
    }

    [Fact]
    public void GoBack_FromNone_DoesNothing()
    {
        var form = new AddFormStateMachine();
        form.GoBack();
        Assert.Equal(AddFormStateMachine.Step.None, form.CurrentStep);
    }

    [Fact]
    public void Reset_ClearsAllState()
    {
        var form = new AddFormStateMachine();
        form.Start();
        form.Submit("GitHub");
        form.Submit("admin");
        form.Submit("pass");

        form.Reset();

        Assert.Equal(AddFormStateMachine.Step.None, form.CurrentStep);
        Assert.False(form.IsActive);
        Assert.Equal("", form.Title);
        Assert.Equal("", form.Username);
        Assert.Equal("", form.Password);
    }

    [Fact]
    public void ParseCurrentInput_Empty_ReturnsEmpty()
    {
        var form = new AddFormStateMachine();
        Assert.Equal("", form.ParseCurrentInput(""));
        Assert.Equal("", form.ParseCurrentInput(null!));
    }

    [Fact]
    public void ParseCurrentInput_NoPipe_ReturnsEntireString()
    {
        var form = new AddFormStateMachine();
        Assert.Equal("标题", form.ParseCurrentInput("标题"));
    }

    [Fact]
    public void ParseCurrentInput_WithPipe_ReturnsAfterLastPipe()
    {
        var form = new AddFormStateMachine();
        Assert.Equal("密码", form.ParseCurrentInput("标题 | 用户名 | 密码"));
    }

    [Fact]
    public void ParseCurrentInput_TrimsWhitespace()
    {
        var form = new AddFormStateMachine();
        Assert.Equal("密码", form.ParseCurrentInput("标题 | 用户名 |  密码  "));
    }

    [Fact]
    public void BuildQueryForStep_Title()
    {
        var form = new AddFormStateMachine();
        form.Start();
        Assert.Equal("pwd add ", form.BuildQueryForStep("pwd"));
    }

    [Fact]
    public void BuildQueryForStep_Username()
    {
        var form = new AddFormStateMachine();
        form.Start();
        form.Submit("GitHub");
        Assert.Equal("pwd add GitHub | ", form.BuildQueryForStep("pwd"));
    }

    [Fact]
    public void BuildQueryForStep_Password()
    {
        var form = new AddFormStateMachine();
        form.Start();
        form.Submit("GitHub");
        form.Submit("admin");
        Assert.Equal("pwd add GitHub | admin | ", form.BuildQueryForStep("pwd"));
    }

    [Fact]
    public void BuildQueryForStep_Confirm()
    {
        var form = new AddFormStateMachine();
        form.Start();
        form.Submit("GitHub");
        form.Submit("admin");
        form.Submit("pass");
        Assert.Equal("pwd add ", form.BuildQueryForStep("pwd"));
    }

    [Fact]
    public void BuildBackQuery_FromUsername()
    {
        var form = new AddFormStateMachine();
        form.Start();
        form.Submit("GitHub");
        Assert.Equal("pwd add ", form.BuildBackQuery("pwd"));
    }

    [Fact]
    public void BuildBackQuery_FromPassword()
    {
        var form = new AddFormStateMachine();
        form.Start();
        form.Submit("GitHub");
        form.Submit("admin");
        Assert.Equal("pwd add GitHub | ", form.BuildBackQuery("pwd"));
    }

    [Fact]
    public void BuildBackQuery_FromConfirm()
    {
        var form = new AddFormStateMachine();
        form.Start();
        form.Submit("GitHub");
        form.Submit("admin");
        form.Submit("pass");
        Assert.Equal("pwd add GitHub | admin | ", form.BuildBackQuery("pwd"));
    }

    [Fact]
    public void FullFlow_SubmitBackResubmit_Works()
    {
        var form = new AddFormStateMachine();

        // 开始
        form.Start();
        Assert.Equal(AddFormStateMachine.Step.Title, form.CurrentStep);

        // 填标题
        form.Submit("GitHub");
        Assert.Equal(AddFormStateMachine.Step.Username, form.CurrentStep);

        // 返回修改标题
        form.GoBack();
        Assert.Equal(AddFormStateMachine.Step.Title, form.CurrentStep);

        // 重新填标题
        form.Submit("GitLab");
        Assert.Equal("GitLab", form.Title);

        // 继续填完
        form.Submit("user");
        form.Submit("pass");
        Assert.Equal(AddFormStateMachine.Step.Confirm, form.CurrentStep);
        Assert.Equal("GitLab", form.Title);
        Assert.Equal("user", form.Username);
        Assert.Equal("pass", form.Password);
    }

    [Fact]
    public void GoBackThenForward_PreservesLaterData()
    {
        var form = new AddFormStateMachine();
        form.Start();
        form.Submit("GitHub");
        form.Submit("admin");
        form.Submit("pass");

        // 从 Confirm 回到 Password
        form.GoBack();
        Assert.Equal(AddFormStateMachine.Step.Password, form.CurrentStep);

        // 重新提交密码
        form.Submit("newpass");
        Assert.Equal(AddFormStateMachine.Step.Confirm, form.CurrentStep);
        Assert.Equal("newpass", form.Password);
    }
}
