using OpenAI;
using OpenAI.Chat;

using System.ClientModel;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;

namespace FlowLauncher.Plugin.VarName;

internal class VarNameService(Settings settings)
{
    private static Uri NormalizeEndpoint(string url)
    {
        if (!url.EndsWith('/')) url += '/';
        return new Uri(url);
    }

    public async Task<List<string>> GenerateAsync(string chinese, CancellationToken token)
    {
        var client = new ChatClient(
            settings.ModelName,
            new ApiKeyCredential(settings.ApiKey),
            new OpenAIClientOptions { Endpoint = NormalizeEndpoint(settings.ApiUrl) });

        var completion = await client.CompleteChatAsync(
            [
                new SystemChatMessage(
                    "你是一个变量名生成器。用户输入中文，你输出3-5个对应的英文变量名候选词（只用英文单词或短语，不要命名规范转换，不要包含符号）。" +
                    "每行一个，不要编号，不要解释，不要输出其它内容。" +
                    "例如输入\"用户管理\"，输出：\nUserManagement\nUserAdmin\nAccountManagement\nUserControl"),
                new UserChatMessage(chinese)
            ],
            new ChatCompletionOptions { MaxOutputTokenCount = 2000 },
            cancellationToken: token);

        return ParseResponse(completion.Value.Content[0].Text);
    }

    internal static List<string> ParseResponse(string text)
    {
        return text
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(line => line.Trim().TrimStart('-', '*', '•', '1', '2', '3', '4', '5', '.', ')'))
            .Where(line => line.Length > 0 && line.All(c => char.IsLetterOrDigit(c) || c == '_' || c == '.' || c == ' '))
            .Select(line => line.Replace(" ", ""))
            .Where(line => line.Length > 0 && char.IsLetter(line[0]))
            .Distinct()
            .ToList();
    }

    public async Task<List<string>> GetModelsAsync(CancellationToken token)
    {
        var endpoint = NormalizeEndpoint(settings.ApiUrl);
        var modelsUrl = new Uri(endpoint, "models").ToString();

        using var http = new HttpClient();
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", settings.ApiKey);

        var response = await http.GetAsync(modelsUrl, token);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(token);
        using var doc = JsonDocument.Parse(json);

        var models = doc.RootElement
            .GetProperty("data")
            .EnumerateArray()
            .Select(e => e.GetProperty("id").GetString() ?? "")
            .Where(id => id.Length > 0)
            .OrderBy(id => id)
            .ToList();

        return models;
    }
}
