using Sdcb.PictureSpeaks.Services.OpenAI;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

namespace Sdcb.PictureSpeaks.Services.Idioms;

public class IdiomService
{
    private Dictionary<string, Idiom> _idioms = null!;
    private string[] _idiomKeys = null!;
    private readonly ChatGPTService _llm;

    public IdiomService(ChatGPTService llm)
    {
        EnsureLoaded();
        _llm = llm;
    }

    private void EnsureLoaded()
    {
        using FileStream fs = File.OpenRead("Data/idiom.json");
        _idioms = JsonSerializer.Deserialize<Idiom[]>(fs)!.ToDictionary(k => k.Word, v => v);
        _idiomKeys = [.. _idioms.Keys];
    }

    public Idiom GetRandomIdiom()
    {
        return _idioms[_idiomKeys[Random.Shared.Next(0, _idiomKeys.Length)]];
    }

    public bool TryGetIdiom(string word, [NotNullWhen(returnValue: true)] out Idiom? idiom)
    {
        return _idioms.TryGetValue(word, out idiom);
    }

    public bool IsIdiom(string word)
    {
        return _idioms.ContainsKey(word);
    }

    public async Task<WordIsIdiomResult> IsIdiomOnline(string word)
    {
        // check whether the word is all chinese characters
        if (!word.All(c => c >= 0x4e00 && c <= 0x9fff))
        {
            return false;
        }

        if (TryGetIdiom(word, out Idiom? idiom))
        {
            return new WordIsIdiomResult(true, idiom.Explanation);
        }
        else
        {
            return await _llm.AskJson<WordIsIdiomResult>(new GptRequest($$"""
                请问“{{word}}”是成语吗?它是什么意思? 请用JSON回答，无需markdown格式或其它解释，格式：
                {
                    "IsIdiom": true|false,
                    "Explanation": "这是一个成语的解释"
                }
                """) { Model = "gpt-35-turbo" });
        }
    }
}

public record struct WordIsIdiomResult(bool IsIdiom, string? Explanation = null)
{
    public static implicit operator bool(WordIsIdiomResult result) => result.IsIdiom;

    public static implicit operator WordIsIdiomResult(bool result) => new(result);
}

public record struct WordExplain(string Word, string? Explanation = null)
{
    public readonly string ToPrompt() => Explanation is null ?
        $"请为成语“{Word}”生成一张符合意境的图片" :
        $"请为成语“{Word}”生成一张符合意境的图片，它的释义为：{Explanation}";
}