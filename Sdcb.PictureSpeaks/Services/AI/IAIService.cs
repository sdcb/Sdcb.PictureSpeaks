using Sdcb.PictureSpeaks.Services.AI.AzureOpenAI;
using System.Text.Json;

namespace Sdcb.PictureSpeaks.Services.AI
{
    public interface IAIService
    {
        Task<T> AskJson<T>(LLMRequest req, int retry = 3);
        IAsyncEnumerable<string> AskStream(LLMRequest req);
        Task<ImageGeneratedResponse> GenerateImage(string idiom);

        public static async Task<T> RetryJson<T>(int retry, Func<Task<string>> action)
        {
            Exception toThrow = null!;
            for (int i = 0; i < retry; ++i)
            {
                string content = await action();
                try
                {
                    return JsonSerializer.Deserialize<T>(content)!;
                }
                catch (Exception e)
                {
                    toThrow = e;
                    Console.WriteLine($"Failed to deserialize {content} to {typeof(T).Name}: {e.Message}");
                }
            }

            throw toThrow;
        }
    }
}