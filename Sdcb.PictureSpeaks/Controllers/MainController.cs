using Azure.AI.OpenAI;
using Azure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Sdcb.PictureSpeaks.Hubs;
using Sdcb.PictureSpeaks.Services.DALL_E3;
using Sdcb.PictureSpeaks.Services.DB;
using ImageGenerationOptions = Sdcb.PictureSpeaks.Services.DALL_E3.ImageGenerationOptions;
using System.Text;

namespace Sdcb.PictureSpeaks.Controllers;

public class MainController(
    DallE3Client dalle3,
    LobbyRepository repo,
    Storage db,
    IHubContext<MainHub, IMainHubClient> hubContext,
    IServiceScopeFactory scopeFactory,
    IWebHostEnvironment webHost) : Controller
{
    private readonly DallE3Client _dalle3 = dalle3;
    private readonly LobbyRepository _repo = repo;
    private readonly Storage _db = db;
    private readonly IHubContext<MainHub, IMainHubClient> _hubContext = hubContext;

    [Route("")]
    public async Task<IActionResult> Index()
    {
        ViewData["Lobbies"] = await _repo.ToListPageViewModel();
        return View();
    }

    [HttpPost]
    public async Task<IActionResult> CreateLobby(string user, string idiom)
    {
        Lobby lobby = await _repo.AddLobby(user, idiom);
        await _hubContext.Clients.All.RefreshLobby();
        _ = GenerateImage(user, idiom, lobby);
        return RedirectToAction("Index");
    }

    private async Task GenerateImage(string user, string idiom, Lobby lobby)
    {
        using IServiceScope scope = scopeFactory.CreateScope();
        LobbyRepository repo = scope.ServiceProvider.GetRequiredService<LobbyRepository>();

        try
        {
            ImageGeneratedResponse resp = await _dalle3.GenerateDallE3Image(new ImageGenerationOptions($"请为成语“{idiom}”生成一张符合意境的图片")
            { 
                Size = "1792x1024",
            });
            LobbyMessage message = await repo.AddImageMessage(lobby.Id, resp.Data[0]);
            await _hubContext.Clients.Group($"lobby-{lobby.Id}").OnNewMessage(message.ToViewModel());
            await _hubContext.Clients.All.OnLobbyStatusChanged(lobby.Id, LobbyStatus.Ready);
            Console.WriteLine($"{user}[{idiom}] --> {resp.Data[0].Url}");
        }
        catch (Exception e)
        {
            Console.WriteLine(e.ToString());
            LobbyMessage message = await repo.AddErrorMessage(lobby.Id, e.Message);
            await _hubContext.Clients.All.OnLobbyStatusChanged(lobby.Id, LobbyStatus.Error);
            await _hubContext.Clients.Group($"lobby-{lobby.Id}").OnNewMessage(message.ToViewModel());
        }
    }

    [HttpGet("image/{id}")]
    public async Task<IActionResult> Image(int id)
    {
        Dalle3Request? req = await _db.Dalle3Request.FindAsync(id);
        if (req is null)
        {
            return NotFound();
        }

        if (req.LocalPath is null)
        {
            return Redirect(req.AzureImageUrl);
        }

        return PhysicalFile(Path.Combine(webHost.WebRootPath, "images", req.LocalPath), "image/png");
    }

    [HttpGet]
    public IActionResult Lobbies()
    {
        return Json(_repo.ToListPageViewModel());
    }

    public async Task<IActionResult> Lobby(int id)
    {
        ViewData["Lobby"] = await _db.Lobby
            .Include(x => x.Messages)
            .SingleAsync(x => x.Id == id);
        return View();
    }

    [HttpPost]
    public async Task<IActionResult> UserGuess(string user, int lobbyId, string guessText)
    {
        LobbyMessage message = await _repo.AddUserGuess(lobbyId, user, guessText);
        await _hubContext.Clients.Group($"lobby-{lobbyId}").OnNewMessage(message.ToViewModel());
        _ = CallAzureOpenAI(lobbyId, user, guessText);
        return Ok();
    }

    private async Task CallAzureOpenAI(int lobbyId, string user, string guessText)
    {
        using IServiceScope scope = scopeFactory.CreateScope();
        IConfiguration config = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        using Storage db = scope.ServiceProvider.GetRequiredService<Storage>();
        LobbyRepository repo = scope.ServiceProvider.GetRequiredService<LobbyRepository>();
        string? endpoint = config["AzureOpenAI:Endpoint"] ?? throw new Exception("Config AzureOpenAI:Endpoint is not set.");
        string? apiKey = config["AzureOpenAI:ApiKey"] ?? throw new Exception("Config AzureOpenAI:ApiKey is not set.");
        string? prompt = config["Prompt"] ?? throw new Exception("Config Prompt is not set.");

        Lobby lobby = await db.Lobby
            .Include(x => x.Messages)
            .SingleAsync(x => x.Id == lobbyId);
        OpenAIClient api = new(new Uri(endpoint), new AzureKeyCredential(apiKey));
        List<ChatMessage> messages =
        [
            new ChatMessage(ChatRole.System, prompt
                .Replace("{{idiom}}", lobby.Idiom)
                .Replace("{{charsCount}}", lobby.Idiom.Length.ToString())
                .Replace("{{revisedPrompt}}", lobby.Dalle3Requests.FirstOrDefault()?.RevisedPrompt)
                ),
            .. lobby.Messages
                .Where(x => x.MessageKind == MessageKind.Text)
                .Select(x => x.User == "AI"
                    ? new ChatMessage(ChatRole.Assistant, x.Message)
                    : new ChatMessage(ChatRole.User, $"{x.User}: {x.Message}")), 
        ];

        LobbyMessage msg = await repo.AddEmptyAIChatMessage(lobbyId);
        await _hubContext.Clients.Group($"lobby-{lobbyId}").OnNewMessage(msg.ToViewModel());

        StringBuilder sb = new();
        await foreach (StreamingChatCompletionsUpdate delta in await api.GetChatCompletionsStreamingAsync(new ChatCompletionsOptions("gpt-4", messages)))
        {
            if (delta.FinishReason == CompletionsFinishReason.Stopped) continue;

            sb.Append(delta.ContentUpdate);
            _ = _hubContext.Clients.Group($"lobby-{lobbyId}").OnMessageStreaming(msg.Id, sb.ToString());
        }
        msg.Message = sb.ToString();
        if (guessText.Contains(lobby.Idiom) || msg.Message.Contains('✅'))
        {
            lobby.LobbyStatus = LobbyStatus.Completed;
            await _hubContext.Clients.All.OnLobbyStatusChanged(lobbyId, lobby.LobbyStatus);
        }
        await db.SaveChangesAsync();
    }

    [HttpPost]
    public async Task<IActionResult> Generate(string prompt)
    {
        ImageGeneratedResponse resp = await _dalle3.GenerateDallE3Image(new ImageGenerationOptions(prompt));
        return Json(resp.Data[0].Url);
    }

    [HttpPost]
    public async Task<IActionResult> Replay(string user, int lobbyId)
    {
        Lobby lobby = await _db.Lobby
            .Include(x => x.Messages)
            .SingleAsync(x => x.Id == lobbyId);
        
        lobby.LobbyStatus = LobbyStatus.Ready;
        LobbyMessage[] toRemoves = lobby.Messages.Where(x => x.MessageKind != MessageKind.Image).ToArray();
        foreach (LobbyMessage toRemove in toRemoves)
        {
            LobbyHistoryMessage historyMsg = toRemove.ToHistoryMessage();
            lobby.HistoryMessages.Add(historyMsg);
            lobby.Messages.Remove(toRemove);
        }        
        await _db.SaveChangesAsync();
        await _hubContext.Clients.All.OnLobbyStatusChanged(lobbyId, lobby.LobbyStatus);

        LobbyMessage message = await _repo.AddUserGuess(lobbyId, user, $"注意，我{user}触发了重玩！");
        await _hubContext.Clients.Group($"lobby-{lobbyId}").OnNewMessage(message.ToViewModel());
        return Ok();
    }
}
