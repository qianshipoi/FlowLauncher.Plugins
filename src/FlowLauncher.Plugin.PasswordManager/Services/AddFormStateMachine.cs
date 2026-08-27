namespace FlowLauncher.Plugin.PasswordManager.Services;

/// <summary>
/// 添加密码的表单状态机。
/// 管理步骤流转和表单数据，不依赖 Flow Launcher API。
/// </summary>
internal sealed class AddFormStateMachine
{
    public enum Step { None, Title, Username, Password, Confirm }

    public Step CurrentStep { get; private set; } = Step.None;
    public string Title { get; private set; } = "";
    public string Username { get; private set; } = "";
    public string Password { get; private set; } = "";

    /// <summary>
    /// 当前是否处于添加流程中
    /// </summary>
    public bool IsActive => CurrentStep != Step.None;

    /// <summary>
    /// 进入添加模式，从标题步骤开始
    /// </summary>
    public void Start()
    {
        CurrentStep = Step.Title;
    }

    /// <summary>
    /// 从管道分隔的 rest 文本中提取当前步骤的输入值。
    /// 例如 "标题 | 用户名" 在 Username 步骤返回 "用户名"。
    /// </summary>
    public string ParseCurrentInput(string rest)
    {
        if (string.IsNullOrEmpty(rest)) return "";
        var pipeIndex = rest.LastIndexOf('|');
        return pipeIndex >= 0 ? rest[(pipeIndex + 1)..].Trim() : rest;
    }

    /// <summary>
    /// 提交当前步骤的值。如果值非空则保存并前进到下一步。
    /// 返回 true 表示成功前进，false 表示值为空被忽略。
    /// </summary>
    public bool Submit(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;

        var trimmed = value.Trim();

        switch (CurrentStep)
        {
            case Step.Title:
                Title = trimmed;
                CurrentStep = Step.Username;
                return true;

            case Step.Username:
                Username = trimmed;
                CurrentStep = Step.Password;
                return true;

            case Step.Password:
                Password = trimmed;
                CurrentStep = Step.Confirm;
                return true;

            default:
                return false;
        }
    }

    /// <summary>
    /// 返回上一步
    /// </summary>
    public void GoBack()
    {
        CurrentStep = CurrentStep switch
        {
            Step.Username => Step.Title,
            Step.Password => Step.Username,
            Step.Confirm => Step.Password,
            _ => CurrentStep,
        };
    }

    /// <summary>
    /// 重置表单，清除所有状态
    /// </summary>
    public void Reset()
    {
        CurrentStep = Step.None;
        Title = "";
        Username = "";
        Password = "";
    }

    /// <summary>
    /// 构建提交后 ChangeQuery 所需的查询文本
    /// </summary>
    public string BuildQueryForStep(string actionKeyword)
    {
        return CurrentStep switch
        {
            Step.Title => $"{actionKeyword} add ",
            Step.Username => $"{actionKeyword} add {Title} | ",
            Step.Password => $"{actionKeyword} add {Title} | {Username} | ",
            Step.Confirm => $"{actionKeyword} add ",
            _ => $"{actionKeyword} ",
        };
    }

    /// <summary>
    /// 构建返回上一步时 ChangeQuery 所需的查询文本
    /// </summary>
    public string BuildBackQuery(string actionKeyword)
    {
        return CurrentStep switch
        {
            Step.Username => $"{actionKeyword} add ",
            Step.Password => $"{actionKeyword} add {Title} | ",
            Step.Confirm => $"{actionKeyword} add {Title} | {Username} | ",
            _ => $"{actionKeyword} add ",
        };
    }
}
