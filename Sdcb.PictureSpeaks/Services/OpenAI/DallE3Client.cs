namespace Sdcb.PictureSpeaks.Services.OpenAI;

public class DallE3Client(OpenAIConfig config)
{
    private readonly OpenAIConfig _config = config;

    public async Task<ImageGeneratedResponse> GenerateDallE3Image(ImageGenerationOptions options)
    {
        Console.Write($"为{options.Prompt}生成图片，大小：{options.Size}...");
        HttpClient client = new()
        {
            BaseAddress = new Uri(_config.Endpoint)
        };
        client.DefaultRequestHeaders.Add("api-key", _config.ApiKey);
        HttpResponseMessage response = await client.PostAsJsonAsync("/openai/deployments/Dalle3/images/generations?api-version=2023-12-01-preview", options);

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
