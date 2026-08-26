namespace FlowLauncher.Plugin.VarName;

public class Settings
{
    public string ModelName { get; set; } = "gpt-3.5-turbo";
    public string ApiUrl { get; set; } = "https://api.openai.com/v1/";
    public string ApiKey { get; set; } = "";

    public bool IsValid()
    {
        return
            !string.IsNullOrWhiteSpace(ModelName) &&
            !string.IsNullOrWhiteSpace(ApiUrl) &&
            !string.IsNullOrWhiteSpace(ApiKey);
    }
}
