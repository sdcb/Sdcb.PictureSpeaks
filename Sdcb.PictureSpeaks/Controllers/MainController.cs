using Azure.AI.OpenAI;
using Azure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Sdcb.PictureSpeaks.Hubs;
using Sdcb.PictureSpeaks.Services.OpenAI;
using Sdcb.PictureSpeaks.Services.DB;
using ImageGenerationOptions = Sdcb.PictureSpeaks.Services.OpenAI.ImageGenerationOptions;
using System.Text;
using Sdcb.PictureSpeaks.Services.Idioms;

namespace Sdcb.PictureSpeaks.Controllers;

public class MainController(
    DallE3Client dalle3,
    LobbyRepository repo,
    Storage db,
    IHubContext<MainHub, IMainHubClient> hubContext,
    IServiceScopeFactory scopeFactory,
    IWebHostEnvironment webHost, 
    IdiomService idiomService) : Controller
{
    private readonly DallE3Client _dalle3 = dalle3;
    private readonly LobbyRepository _repo = repo;
    private readonly Storage _db = db;
    private readonly IHubContext<MainHub, IMainHubClient> _hubContext = hubContext;
    private readonly IdiomService _idiomService = idiomService;

    [Route("")]
    public async Task<IActionResult> Index()
    {
        ViewData["Lobbies"] = await _repo.ToListPageViewModel();
        return View();
    }

    [HttpPost]
    public async Task<IActionResult> CreateLobby(string user, string idiom)
    {
        WordIsIdiomResult res = await _idiomService.IsIdiomOnline(idiom);
        if (!res)
        {
            return BadRequest($"错误，你的输入“{idiom}”不是成语！");
        }
        Lobby lobby = await _repo.AddLobby(user, idiom);
        await _hubContext.Clients.All.RefreshLobby();
        _ = GenerateImage(user, lobby, new WordExplain(idiom, res.Explanation!), markError: true);
        return RedirectToAction(nameof(Lobby), new { id = lobby.Id });
    }

    [HttpPost]
    public async Task<IActionResult> CreateRandomLobby(string user)
    {
        Idiom idiom = _idiomService.GetRandomIdiom();
        Lobby lobby = await _repo.AddLobby(user, idiom.Word);
        await _hubContext.Clients.All.RefreshLobby();
        _ = GenerateImage(user, lobby, idiom.ToIdiomExplain(), markError: true);
        return RedirectToAction(nameof(Lobby), new { id = lobby.Id });
    }

    private async Task GenerateImage(string user, Lobby lobby, WordExplain idiom, bool markError)
    {
        using IServiceScope scope = scopeFactory.CreateScope();
        LobbyRepository repo = scope.ServiceProvider.GetRequiredService<LobbyRepository>();

        try
        {
            ImageGeneratedResponse resp = await _dalle3.GenerateDallE3Image(new ImageGenerationOptions(idiom.ToPrompt())
            {
                Size = "1792x1024",
            });
            LobbyMessage message = await repo.AddImageMessage(lobby.Id, resp.Data[0]);
            await _hubContext.Clients.Group($"lobby-{lobby.Id}").OnNewMessage(message.ToViewModel());
            await _hubContext.Clients.All.OnLobbyStatusChanged(lobby.Id, LobbyStatus.Ready);
        }
        catch (Exception e)
        {
            Console.WriteLine(e.ToString());
            LobbyMessage message = await repo.AddErrorMessage(lobby.Id, e.Message, markError);
            if (markError)
            {
                await _hubContext.Clients.All.OnLobbyStatusChanged(lobby.Id, LobbyStatus.Error);
            }
            
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
        string? prompt = config["Prompt"] ?? throw new Exception("Config Prompt is not set.");
        ChatGPTService llm = scope.ServiceProvider.GetRequiredService<ChatGPTService>();

        Lobby lobby = await db.Lobby
            .Include(x => x.Messages)
            .SingleAsync(x => x.Id == lobbyId);

        LobbyMessage msg = await repo.AddEmptyAIChatMessage(lobbyId);
        await _hubContext.Clients.Group($"lobby-{lobbyId}").OnNewMessage(msg.ToViewModel());

        string systemPrompt = prompt
                .Replace("{{idiom}}", lobby.Idiom)
                .Replace("{{charsCount}}", lobby.Idiom.Length.ToString())
                .Replace("{{revisedPrompt}}", lobby.Dalle3Requests.FirstOrDefault()?.RevisedPrompt);
        ChatMessage[] historyPrompt = lobby.Messages
            .OrderByDescending(x => x.Id)
            .Take(20)
            .OrderBy(x => x.Id)
            .Where(x => x.MessageKind == MessageKind.Text)
            .Select(x => x.User == "AI"
                ? new ChatMessage(ChatRole.Assistant, x.Message)
                : new ChatMessage(ChatRole.User, $"{x.User}: {x.Message}"))
            .ToArray();

        bool haveAction = false;
        await foreach (string full in llm.AskStream(new GptRequest(systemPrompt, historyPrompt)).DeltaToFull())
        {
            _ = _hubContext.Clients.Group($"lobby-{lobbyId}").OnMessageStreaming(msg.Id, full);
            msg.Message = full;

            if (haveAction) continue;
            if (msg.Message.StartsWith('✅') || guessText.Contains(lobby.Idiom))
            {
                lobby.LobbyStatus = LobbyStatus.Completed;
                await _hubContext.Clients.All.OnLobbyStatusChanged(lobbyId, lobby.RealStatus);
                haveAction = true;
            }
            else if (msg.Message.StartsWith("🖼🖼"))
            {
                _ = GenerateImage(user, lobby, new WordExplain(lobby.Idiom), markError: false);
                haveAction = true;
            }
        }

        await db.SaveChangesAsync();
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
        await _hubContext.Clients.All.OnLobbyStatusChanged(lobbyId, lobby.RealStatus);

        LobbyMessage message = await _repo.AddUserGuess(lobbyId, user, $"注意，我{user}触发了重玩！");
        await _hubContext.Clients.Group($"lobby-{lobbyId}").OnNewMessage(message.ToViewModel());
        return Ok();
    }

    [HttpPost]
    public async Task<IActionResult> Regenerate(string user, int lobbyId)
    {
        Lobby lobby = await _db.Lobby
            .Include(x => x.Messages)
            .SingleAsync(x => x.Id == lobbyId);
        if (lobby.RealStatus != LobbyStatus.Error)
        {
            return BadRequest();
        }

        lobby.LobbyStatus = LobbyStatus.Pending;
        LobbyMessage[] toRemoves = lobby.Messages.Where(x => x.MessageKind != MessageKind.Image).ToArray();
        foreach (LobbyMessage toRemove in toRemoves)
        {
            LobbyHistoryMessage historyMsg = toRemove.ToHistoryMessage();
            lobby.HistoryMessages.Add(historyMsg);
            lobby.Messages.Remove(toRemove);
        }
        await _db.SaveChangesAsync();
        await _hubContext.Clients.All.OnLobbyStatusChanged(lobbyId, lobby.RealStatus);

        LobbyMessage message = await _repo.AddUserGuess(lobbyId, user, $"注意，我{user}触发了重新生成！");
        await _hubContext.Clients.Group($"lobby-{lobbyId}").OnNewMessage(message.ToViewModel());

        _ = GenerateImage(user, lobby, new WordExplain(lobby.Idiom), markError: true);

        return Ok();
    }
}
