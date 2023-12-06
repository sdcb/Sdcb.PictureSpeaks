using System.Diagnostics.CodeAnalysis;

namespace Sdcb.PictureSpeaks.Services.OpenAI;

public record OpenAIConfig
{
    public OpenAIConfig(IConfiguration config)
    {
        IConfigurationSection section = config.GetRequiredSection("AzureOpenAI");
        Endpoint = section[nameof(Endpoint)] ?? throw new Exception("AzureOpenAI:Endpoint is not set.");
        ApiKey = section[nameof(ApiKey)] ?? throw new Exception("AzureOpenAI:ApiKey is not set.");
    }

    public string Endpoint { get; } = null!;
    public string ApiKey { get; } = null!;
}
