using Azure;
using Azure.AI.OpenAI;
using OpenAI.Chat;
using System.ClientModel;
using System.Text;
using System.Text.Json;
using System.Text.Json.Schema;

namespace Sdcb.PictureSpeaks.Services.AI.AzureOpenAI;

public class AzureOpenAIService(AzureOpenAIConfig config) : IAIService
{
    private readonly ChatClient _c = new AzureOpenAIClient(new Uri(config.Endpoint), new AzureKeyCredential(config.ApiKey)).GetChatClient(config.Model);

    public async IAsyncEnumerable<string> AskStream(ChatMessage[] chatMessages)
    {
        await foreach (StreamingChatCompletionUpdate delta in _c.CompleteChatStreamingAsync(chatMessages))
        {
            if (delta.FinishReason == ChatFinishReason.Stop) continue;
            if (delta.ContentUpdate.Count > 0)
            {
                yield return delta.ContentUpdate[0].Text;
            }
        }
    }

    public async Task<T> AskJson<T>(ChatMessage[] chatMessages, int retry = 3)
    {
        return await IAIService.RetryJson<T>(retry, async () =>
        {
            ClientResult<ChatCompletion> completion = await _c.CompleteChatAsync(chatMessages, new ChatCompletionOptions()
            {
                ResponseFormat = ChatResponseFormat.CreateJsonSchemaFormat(typeof(T).Name, BinaryData.FromBytes(JsonSerializer.SerializeToUtf8Bytes(JsonSchemaExporter.GetJsonSchemaAsNode(JsonSerializerOptions.Default, typeof(T), new JsonSchemaExporterOptions()
                {
                    TreatNullObliviousAsNonNullable = true
                }))))
            });
            string content = completion.Value.Content[0].Text;
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

    public static async Task<string> GetFinal(this IAsyncEnumerable<string> deltas)
    {
        StringBuilder full = new();
        await foreach (string delta in deltas)
        {
            full.Append(delta);
        }
        return full.ToString();
    }
}
