using System.Text.Json.Serialization;

namespace Sdcb.PictureSpeaks.Services.Idioms;

public class Idiom
{
    [JsonPropertyName("derivation")]
    public required string Derivation { get; set; }

    [JsonPropertyName("example")]
    public required string Example { get; set; }

    [JsonPropertyName("explanation")]
    public required string Explanation { get; set; }

    [JsonPropertyName("pinyin")]
    public required string Pinyin { get; set; }

    [JsonPropertyName("word")]
    public required string Word { get; set; }

    [JsonPropertyName("abbreviation")]
    public required string Abbreviation { get; set; }

    public WordExplain ToIdiomExplain()
    {
        return new WordExplain
        {
            Word = Word,
            Explanation = Explanation,
        };
    }
}
