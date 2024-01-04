using Sdcb.DashScope;
using Sdcb.DashScope.StableDiffusion;
using Sdcb.DashScope.TextGeneration;
using Sdcb.PictureSpeaks.Services.AI.AzureOpenAI;
using System.Diagnostics;
using System.Text.Json;

namespace Sdcb.PictureSpeaks.Services.AI.DashScope;

public class DashScopeService : IAIService
{
    private readonly DashScopeClient _c;

    public DashScopeService(IConfiguration config)
    {
        if (config["DashScope:ApiKey"] == null)
        {
            throw new Exception("DashScope:ApiKey is not set");
        }

        _c = new DashScopeClient(config["DashScope:ApiKey"]!);
    }

    public async Task<T> AskJson<T>(LLMRequest req, int retry = 3)
    {
        (string model, IReadOnlyList<ChatMessage> messages, ChatParameters parameters) = req.ToDashScope();

        return await IAIService.RetryJson<T>(retry, async () =>
        {
            ResponseWrapper<ChatOutput, ChatTokenUsage> resp = await _c.TextGeneration.Chat(model, messages, parameters);
            return resp.Output.Text.Replace("```json", "").Replace("```", "");
        });
    }

    public async IAsyncEnumerable<string> AskStream(LLMRequest req)
    {
        (string model, IReadOnlyList<ChatMessage> messages, ChatParameters parameters) = req.ToDashScope();
        parameters.IncrementalOutput = true;
        await foreach (var resp in _c.TextGeneration.ChatStreamed(model, messages, parameters))
        {
            yield return resp.Output.Text;
        }
    }

    private async Task<string> Chat(List<ChatMessage> messages)
    {
        string finalResponse = "";
        await foreach (ResponseWrapper<ChatOutput, ChatTokenUsage> item in _c.TextGeneration.ChatStreamed("qwen-max", messages, new ChatParameters
        {
            Seed = (ulong)Random.Shared.Next(),
        }))
        {
            finalResponse = item.Output.Text;
        }
        return finalResponse;
    }

    public async Task<ImageGeneratedResponse> GenerateImage(string idiom)
    {
        Stopwatch sw = Stopwatch.StartNew();
        Console.WriteLine($"{idiom}[{sw.ElapsedMilliseconds}]: 生成图片开始...");

        List<ChatMessage> messages =
        [
            ChatMessage.FromSystem("你是智能AI助理，并仔细遵循用户的问题，并认真回复。"),
            ChatMessage.FromUser($"""
		    请用尽可能详尽的文字描述一幅画面，让人看到这幅画面，能联想到这个成语：{idiom}
		    """),
        ];
        messages =
        [
            .. messages,
            ChatMessage.FromAssistant(await Chat(messages)),
            ChatMessage.FromUser("""
		    请根据上面的文字场景描述，生成stable diffusion的提示词，需要有正面/负面提示词（使用英语），使用这样的JSON格式：
		    {
		      "prompt": "...", 
		      "negative_prompt": "..." 
		    }
		    为了帮你理解stable diffusion提示词，这里给你一个示例
		    这是一个正面提示词示例，它表示画面中应该存在的元素：standing, ultra detailed, official art, 4k 8k wallpaper, soft light and shadow, hand detail, eye high detail, 8K, (best quality:1.5), pastel color, soft focus, masterpiece, studio, hair high detail, (pure background:1.2), (head fully visible, full body shot)
		    这是一个负面提示词示例，它表示页面中应该避免存在的元素：EasyNegative, nsfw,(low quality, worst quality:1.4),lamp, missing shoe, missing head,mutated hands and fingers,deformed,bad anatomy,extra limb,ugly,poorly drawn hands,disconnected limbs,missing limb,missing head,camera
		    """),
        ];
        JsonDocument json = JsonDocument.Parse(await Chat(messages));

        Text2ImagePrompt prompt = new()
        {
            Prompt = json.RootElement.GetProperty("prompt").GetString()!,
            NegativePrompt = json.RootElement.GetProperty("negative_prompt").GetString()!
        };
        Console.WriteLine($"{idiom}[{sw.ElapsedMilliseconds}]: {prompt}");
        DashScopeTask task = await _c.WanXiang.Text2Image(prompt, new Text2ImageParams 
        { 
            N = 2, 
            Size = "1280*720" 
        }, model: "wanx-v1");

        while (true)
        {
            TaskStatusResponse status = await _c.QueryTaskStatus(task.TaskId);
            if (status.TaskStatus == DashScopeTaskStatus.Succeeded)
            {
                SuccessTaskResponse success = status.AsSuccess();
                ImageGeneratedResponse result = new ()
                {
                    Created = 0, 
                    Data = success.Results
                        .Where(x => x.IsSuccess)
                        .Select(x => new Datum
                        {
                            Url = x.Url!,
                            RevisedPrompt = prompt.Prompt
                        }).ToList()
                };
                string urls = string.Join("\n", result.Data.Select(x => x.Url));
                Console.WriteLine($"{idiom}[{sw.ElapsedMilliseconds}]: 生成成功，图片地址：\n{urls}");
                return result;
            }
            else if (status.TaskStatus == DashScopeTaskStatus.Failed)
            {
                Console.WriteLine($"{idiom}[{sw.ElapsedMilliseconds}]: 图片生成失败，原因：{status.AsFailed().Message}");
                throw new Exception(status.AsFailed().Message);
            }
            await Task.Delay(1000);
        }
    }
}
