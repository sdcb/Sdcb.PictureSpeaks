using Azure;
using Azure.AI.OpenAI;
using System.Text;
using System.Text.Json;

namespace Sdcb.PictureSpeaks.Services.OpenAI;

public class ChatGPTService(OpenAIConfig config)
{
    public async IAsyncEnumerable<string> AskStream(GptRequest req)
    {
        OpenAIClient api = new(new Uri(config.Endpoint), new AzureKeyCredential(config.ApiKey));

        await foreach (StreamingChatCompletionsUpdate delta in await api.GetChatCompletionsStreamingAsync(new ChatCompletionsOptions(req.Model, req.AllMessages)))
        {
            if (delta.FinishReason == CompletionsFinishReason.Stopped) continue;
            yield return delta.ContentUpdate;
        }
    }

    public async Task<T> AskJson<T>(GptRequest req, int retry = 3)
    {
        OpenAIClient api = new(new Uri(config.Endpoint), new AzureKeyCredential(config.ApiKey));

        Exception toThrow = null!;
        for (int i = 0; i < retry; ++i)
        {
            Response<ChatCompletions> completion = await api.GetChatCompletionsAsync(new ChatCompletionsOptions(req.Model, req.AllMessages));
            string content = completion.Value.Choices[0].Message.Content.Replace("```json", "").Replace("```", "");
            try
            {
                return JsonSerializer.Deserialize<T>(content)!;
            }
            catch (Exception e)
            {
                toThrow = e;
                Console.WriteLine($"Failed to deserialize {content} to {typeof(T).Name}: {e.Message}");
            }
        }

        throw toThrow;
    }
}

public static class ChatGPTServiceExtensions
{
    public static async IAsyncEnumerable<string> DeltaToFull(this IAsyncEnumerable<string> deltas)
    {
        StringBuilder full = new();
        await foreach (string delta in deltas)
        {
            full.Append(delta);
            yield return full.ToString();
        }
    }
}

public record GptRequest(string? SystemPrompt = null, params ChatMessage[] Messages)
{
    public string Model { get; init; } = "gpt-4";

    public ChatMessage[] AllMessages => SystemPrompt is null ? Messages :
    [
        new ChatMessage(ChatRole.System, SystemPrompt),
        .. Messages
    ];
}
