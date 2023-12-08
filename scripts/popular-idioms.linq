<Query Kind="Statements">
  <NuGetReference Prerelease="true">Azure.AI.OpenAI</NuGetReference>
  <Namespace>Azure.AI.OpenAI</Namespace>
  <Namespace>Azure</Namespace>
  <Namespace>System.Text.Json.Serialization</Namespace>
  <Namespace>System.Text.Json</Namespace>
  <Namespace>System.Threading.Tasks</Namespace>
  <Namespace>System.Text.Encodings.Web</Namespace>
</Query>

string rootPath = new FileInfo(Util.CurrentQueryPath).Directory!.Parent!.ToString();
Idiom[] allWords = JsonSerializer.Deserialize<Idiom[]>(File.ReadAllBytes(Path.Combine(rootPath, @"Sdcb.PictureSpeaks\Data\idiom.json")))!;
ChatgptService llm = new();
int refCount = 3;
int batchCount = 20;
int batchIndex = 0;
DumpContainer dc = new DumpContainer().Dump("状态");
Idiom[][] batches = allWords.Chunk(batchCount).ToArray();
foreach (Idiom[] batched in batches)
{
	string jsonPath = Path.Combine(rootPath, $@"scripts\temp\{batchIndex}.json");
	if (File.Exists(jsonPath))
	{
		batchIndex++;
		continue;
	}
	
	string[] words = batched.Select(x => x.Word).ToArray();
	Task<Dictionary<string, int>>[] scoreTasks = Enumerable.Range(0, refCount).Select(x => llm.QueryGPT(words, QueryCancelToken)).ToArray();
	Task.WaitAll(scoreTasks);
	Dictionary<string, int> avg = CalculateAverageScores(scoreTasks.Select(x => x.Result).ToArray());
	File.WriteAllText(Path.Combine(rootPath, jsonPath),
		JsonSerializer.Serialize(avg, new JsonSerializerOptions()
		{
			Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
		}));
	batchIndex++;
	dc.Content = new
	{
		Completed = batchIndex * batchCount, 
		All = allWords.Length, 
		Percent = (1.0 * batchIndex * batchCount / allWords.Length).ToString("P2")
	};
}

Dictionary<string, int> CalculateAverageScores(Dictionary<string, int>[] scoreDictionaries)
{
	// 初始化用于累加分数的字典
	Dictionary<string, int> sumScores = new Dictionary<string, int>();

	// 记录引用的数量以计算平均值
	int refCount = scoreDictionaries.Length;

	// 累加每个字典的分数
	foreach (var scores in scoreDictionaries)
	{
		foreach (var kvp in scores)
		{
			if (sumScores.ContainsKey(kvp.Key))
			{
				sumScores[kvp.Key] += kvp.Value;
			}
			else
			{
				sumScores[kvp.Key] = kvp.Value;
			}
		}
	}

	// 计算平均分数
	Dictionary<string, int> avgScores = sumScores.ToDictionary(kvp => kvp.Key, kvp => kvp.Value / refCount);

	return avgScores;
}

public class ChatgptService
{
	OpenAIClient api = new OpenAIClient(new Uri($"https://{Util.GetPassword("azure-ai-resource")}.openai.azure.com/"), new AzureKeyCredential(Util.GetPassword("azure-ai-key")));
	ChatMessage systemPrompt = new ChatMessage(ChatRole.System, """
		你是冷门成语鉴定程序，请鉴定用户发送的成语是否为冷门成语
		请使用0（非常冷门）~100（非常常用）分为用户的这些成语常用程序打分
		用户会发送一个成语的列表，请鉴定这个列表的成语是否冷门，然后返回一个JSON格式，像这样：
		{
		  "成语1": 10, 
		  "成语2": 20 
		}
		注意：请直接输出JSON，不需要任何其它解释，也不要输出markdown格式！
		""");
	DumpContainer errorDc = new DumpContainer().Dump("chatgpt-错误");
	int errorCount = 0;
	public async Task<Dictionary<string, int>> QueryGPT(string[] words, CancellationToken ct = default)
	{
		string jsonInput = JsonSerializer.Serialize(words, new JsonSerializerOptions() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping });
		ChatMessage[] messages = [systemPrompt, new ChatMessage(ChatRole.User, jsonInput)];

		while (true)
		{
			string respText = null!;
			try
			{
				Response<ChatCompletions> resp = await api.GetChatCompletionsAsync(new ChatCompletionsOptions("gpt-4", messages), ct);
				respText = resp.Value.Choices[0].Message.Content.Replace("```json", "").Replace("```", "");
				Dictionary<string, int> result = JsonSerializer.Deserialize<Dictionary<string, int>>(respText)!;
				if (result.Keys.ToHashSet().SetEquals(words)) return result;
			}
			catch (TaskCanceledException) { throw; }
			catch (Exception ex)
			{
				errorCount++;
				errorDc.Content = new
				{
					Count = errorCount,
					LastError = ex.Message,
					Sample = respText,
				};
			}
		}
	}
}

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
}