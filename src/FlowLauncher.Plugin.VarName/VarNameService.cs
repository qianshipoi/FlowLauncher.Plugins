using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;

namespace FlowLauncher.Plugin.VarName;

public class VarNameService(Settings settings)
{
    private static Uri NormalizeEndpoint(string url)
    {
        if (!url.EndsWith('/')) url += '/';
        return new Uri(url);
    }

    public async Task<List<string>> GenerateAsync(string chinese, CancellationToken token)
    {
        var endpoint = NormalizeEndpoint(settings.ApiUrl);
        var url = new Uri(endpoint, $"chat/completions").ToString();

        var requestBody = new
        {
            model = settings.ModelName,
            max_completion_tokens = 2000,
            thinking = new
            {
                type = "disabled"  
            },
            messages = new object[]
            {
                new { role = "system", content =
                    "你是一个变量名生成器。用户输入中文，你输出3-5个对应的英文变量名候选词（只用英文单词或短语，不要命名规范转换，不要包含符号）。" +
                    "每行一个，不要编号，不要解释，不要输出其它内容。" +
                    "例如输入\"用户管理\"，输出：\nUserManagement\nUserAdmin\nAccountManagement\nUserControl" },
                new { role = "user", content = chinese }
            }
        };

        using var http = new HttpClient();
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", settings.GetPlainApiKey());

        var jsonContent = new StringContent(
            JsonSerializer.Serialize(requestBody),
            System.Text.Encoding.UTF8,
            "application/json");

        var response = await http.PostAsync(url, jsonContent, token);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(token);
        using var doc = JsonDocument.Parse(json);

        var text = doc.RootElement
            .GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString() ?? "";

        return ParseResponse(text);
    }

    public static List<string> ParseResponse(string text)
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
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", settings.GetPlainApiKey());

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
