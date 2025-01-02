using OpenAI.Chat;
using Sdcb.PictureSpeaks.Services.AI;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

namespace Sdcb.PictureSpeaks.Services.Idioms;

public class IdiomService
{
    private Dictionary<string, Idiom> _idioms = null!;
    private string[] _idiomKeys = null!;
    private string[] _idiomCommonKeys = null!;
    private readonly IAIService _llm;

    public IdiomService(IAIService llm)
    {
        EnsureLoaded();
        _llm = llm;
    }

    private void EnsureLoaded()
    {
        _idioms = JsonSerializer.Deserialize<Idiom[]>(File.ReadAllBytes("Data/idiom.json"))!.ToDictionary(k => k.Word, v => v);
        _idiomKeys = [.. _idioms.Keys];
        _idiomCommonKeys = File.ReadAllLines("Data/common.txt");
    }

    public Idiom GetRandomIdiom()
    {
        return _idioms[_idiomCommonKeys[Random.Shared.Next(0, _idiomCommonKeys.Length)]];
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
        if (word.Length > 20) return false;

        // check whether the word is all chinese characters
        if (!word.All(c => c >= 0x4e00 && c <= 0x9fff || c == ',' || c == '，'))
        {
            return false;
        }

        if (TryGetIdiom(word, out Idiom? idiom))
        {
            return new WordIsIdiomResult(true, idiom.Explanation);
        }
        else
        {
            return await _llm.AskJson<WordIsIdiomResult>(
                [ChatMessage.CreateUserMessage($$"""
                    请问“{{word}}”是成语吗?它是什么意思? 请用JSON回答，无需markdown格式或其它解释，格式：
                    {
                        "IsIdiom": true|false,
                        "Explanation": "这是一个成语的解释"
                    }
                    """)
                ]);
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
}