using OpenAI;
using OpenAI.Chat;

using System.ClientModel;

namespace FlowLauncher.Plugin.Translator;

internal interface ITranslator
{
    Task<string> TranslateAsync(string text, string fromLanguage, string toLanguage, CancellationToken token);
}

internal class AiTanslator(Settings settings) : ITranslator
{
    public async Task<string> TranslateAsync(string text, string fromLanguage, string toLanguage, CancellationToken token)
    {
        var client = new ChatClient(
            settings.ModelName,
            new ApiKeyCredential(settings.ApiKey),
            new OpenAIClientOptions()
            {
                Endpoint = new Uri(settings.ApiUrl),
            });

        ChatCompletion completion = await client.CompleteChatAsync(
            [new SystemChatMessage("你是一个翻译官，会将我输入的文本自动转为英文，不要回复其它文案，只需要回复翻译后的文案"), new UserChatMessage(text)]);

        return completion.Content[0].Text;
    }
}
