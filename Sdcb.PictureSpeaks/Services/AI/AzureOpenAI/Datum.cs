using System.Text.Json.Serialization;

namespace Sdcb.PictureSpeaks.Services.AI.AzureOpenAI;

public class Datum
{
    [JsonPropertyName("revised_prompt")]
    public required string RevisedPrompt { get; init; }

    [JsonPropertyName("url")]
    public required string Url { get; init; }
}
