using Azure.AI.OpenAI;
using Sdcb.DashScope.TextGeneration;

namespace Sdcb.PictureSpeaks.Services.AI;

public record LLMRequest(string? SystemPrompt = null, params ChatMessage[] Messages)
{
    public bool IsStrongModel { get; init; } = true;

    public ChatMessage[] AllMessages => SystemPrompt is null ? Messages :
    [
        ChatMessage.FromSystem(SystemPrompt),
        .. Messages
    ];

    public ChatCompletionsOptions ToAzureOpenAI()
    {
        string model = IsStrongModel ? "gpt-4" : "gpt-35-turbo";
        return new ChatCompletionsOptions(model, AllMessages.Select(x => (ChatRequestMessage)(x.Role switch
        {
            "system" => new ChatRequestSystemMessage(x.Content),
            "user" => new ChatRequestUserMessage(x.Content),
            "assistant" => new ChatRequestAssistantMessage(x.Content),
            _ => throw new ArgumentOutOfRangeException(nameof(x.Role))
        })));
    }

    public (string model, IReadOnlyList<ChatMessage> messages, ChatParameters parameters) ToDashScope()
    {
        string model = IsStrongModel ? "qwen-max" : "qwen-max"; // qwen-turbo不行
        IReadOnlyList<ChatMessage> messages = DashScopeChatMessageFilter(AllMessages);
        ChatParameters parameters = new()
        {
            Seed = (ulong)Random.Shared.Next(),
        };
        return (model, messages, parameters);
    }

    public static IReadOnlyList<ChatMessage> DashScopeChatMessageFilter(ChatMessage[] messages)
    {
        if (messages == null || messages.Length == 0)
        {
            return messages!;
        }

        List<ChatMessage> filteredMessages = [];

        // Start with the last message
        string lastRole = null!;
        bool lastSkipped = false;
        for (int i = messages.Length - 1; i >= 0; i--)
        {
            if (!lastSkipped)
            {
                if (messages[i].Role != "user")
                {
                    continue;
                }
                else
                {
                    lastSkipped = true;
                }
            }

            if (lastRole == messages[i].Role)
            {
                continue;
            }

            filteredMessages.Add(messages[i]);
            lastRole = messages[i].Role;
        }

        // Reverse the list to maintain the original order
        filteredMessages.Reverse();

        // Ensure first message is user message
        var toRemove = new List<ChatMessage>();
        for (int i = 0; i < filteredMessages.Count; i++)
        {
            if (filteredMessages[i].Role != "system" && filteredMessages[i].Role != "user")
            {
                toRemove.Add(filteredMessages[i]);
            }
            else if (filteredMessages[i].Role == "user")
            {
                break;
            }
        }
        foreach (var item in toRemove)
        {
            filteredMessages.Remove(item);
        }

        return filteredMessages;
    }
}
