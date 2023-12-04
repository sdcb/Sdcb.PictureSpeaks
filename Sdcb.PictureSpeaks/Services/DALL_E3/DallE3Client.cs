namespace Sdcb.PictureSpeaks.Services.DALL_E3;

public class DallE3Client
{
    private readonly string _endpoint = null!;
    private readonly string _apiKey = null!;

    public DallE3Client(IConfiguration config)
    {
        _endpoint = config["AzureOpenAI:Endpoint"]!;
        if (_endpoint == null)
        {
            throw new Exception("Config AzureOpenAI:Endpoint is not set.");
        }
        
        _apiKey = config["AzureOpenAI:ApiKey"]!;
        if (_apiKey == null)
        {
            throw new Exception("Config AzureOpenAI:ApiKey is not set.");
        }
    }

    public async Task<ImageGeneratedResponse> GenerateDallE3Image(ImageGenerationOptions options)
    {
        HttpClient client = new()
        {
            BaseAddress = new Uri(_endpoint)
        };
        client.DefaultRequestHeaders.Add("api-key", _apiKey);
        HttpResponseMessage response = await client.PostAsJsonAsync("/openai/deployments/Dalle3/images/generations?api-version=2023-12-01-preview", options);

        if (response.IsSuccessStatusCode)
        {
            return (await response.Content.ReadFromJsonAsync<ImageGeneratedResponse>())!;
        }
        else
        {
            throw new DallE3Exception(await response.Content.ReadAsStringAsync());
        }
    }
}
