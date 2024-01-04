using Azure;
using Azure.AI.OpenAI;
using System.Text;
using System.Text.Json;

namespace Sdcb.PictureSpeaks.Services.AI.AzureOpenAI;

public class AzureOpenAIService(AzureOpenAIConfig config) : IAIService
{
    private readonly OpenAIClient _c = new(new Uri(config.Endpoint), new AzureKeyCredential(config.ApiKey));

    public async IAsyncEnumerable<string> AskStream(LLMRequest req)
    {
        await foreach (StreamingChatCompletionsUpdate delta in await _c.GetChatCompletionsStreamingAsync(req.ToAzureOpenAI()))
        {
            if (delta.FinishReason == CompletionsFinishReason.Stopped) continue;
            yield return delta.ContentUpdate;
        }
    }

    public async Task<T> AskJson<T>(LLMRequest req, int retry = 3)
    {
        ChatCompletionsOptions llmReq = req.ToAzureOpenAI();
        llmReq.ResponseFormat = ChatCompletionsResponseFormat.JsonObject;

        return await IAIService.RetryJson<T>(retry, async () =>
        {
            Response<ChatCompletions> completion = await _c.GetChatCompletionsAsync(llmReq);
            string content = completion.Value.Choices[0].Message.Content;
            return content;
        });
    }

    public async Task<ImageGeneratedResponse> GenerateImage(string idiom)
    {
        ImageGenerationOptions req = new($"""
            请为成语“{idiom}”生成一张符合意境的图片
            """, "1792x1024");
        Console.Write($"为{idiom}生成图片，大小：{req.Size}...");
        HttpClient client = new()
        {
            BaseAddress = new Uri(config.Endpoint)
        };
        client.DefaultRequestHeaders.Add("api-key", config.ApiKey);
        HttpResponseMessage response = await client.PostAsJsonAsync("/openai/deployments/Dalle3/images/generations?api-version=2023-12-01-preview", req);

        if (response.IsSuccessStatusCode)
        {
            ImageGeneratedResponse resp = (await response.Content.ReadFromJsonAsync<ImageGeneratedResponse>())!;
            Console.WriteLine($"生成成功，图片地址：{resp.Data[0].Url}");
            return resp;
        }
        else
        {
            throw new DallE3Exception(await response.Content.ReadAsStringAsync());
        }
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
