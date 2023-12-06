using System.Text.Json.Serialization;

namespace Sdcb.PictureSpeaks.Services.OpenAI;

public class ImageGeneratedResponse
{
    [JsonPropertyName("created")]
    public long Created { get; init; }

    [JsonPropertyName("data")]
    public required List<Datum> Data { get; init; }
}
