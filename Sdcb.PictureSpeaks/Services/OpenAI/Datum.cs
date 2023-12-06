using System.Text.Json.Serialization;

namespace Sdcb.PictureSpeaks.Services.OpenAI;

public class Datum
{
    [JsonPropertyName("revised_prompt")]
    public required string RevisedPrompt { get; init; }

    [JsonPropertyName("url")]
    public required string Url { get; init; }
}
