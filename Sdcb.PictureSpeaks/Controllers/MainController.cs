using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Sdcb.PictureSpeaks.Hubs;
using Sdcb.PictureSpeaks.Services.DB;
using Sdcb.PictureSpeaks.Services.Idioms;
using Sdcb.PictureSpeaks.Services.AI.AzureOpenAI;
using Sdcb.PictureSpeaks.Services.AI;
using OpenAI.Chat;

namespace Sdcb.PictureSpeaks.Controllers;

public class MainController(
    LobbyRepository repo,
    Storage db,
    IHubContext<MainHub, IMainHubClient> hubContext,
    IServiceScopeFactory scopeFactory,
    IWebHostEnvironment webHost,
    IdiomService idiomService,
    IAIService ai) : Controller
{
    private readonly LobbyRepository _repo = repo;
    private readonly Storage _db = db;
    private readonly IHubContext<MainHub, IMainHubClient> _hubContext = hubContext;
    private readonly IdiomService _idiomService = idiomService;
    private readonly IAIService _ai = ai;

    [Route("")]
    public async Task<IActionResult> Index()
    {
        ViewData["Lobbies"] = await _repo.ToListPageViewModel();
        return View();
    }

    [HttpPost]
    public async Task<IActionResult> CreateLobby(string user, string idiom)
    {
        if (user.Length > 10)
        {
            return BadRequest("错误，你的名字太长了！");
        }

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
        if (user.Length > 10)
        {
            return BadRequest("错误，你的名字太长了！");
        }

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
            ImageGeneratedResponse resp = await _ai.GenerateImage(idiom.Word);
            foreach (Datum data in resp.Data)
            {
                LobbyMessage message = await repo.AddImageMessage(lobby.Id, data);
                await _hubContext.Clients.Group($"lobby-{lobby.Id}").OnNewMessage(message.ToViewModel());
            }

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

    public async Task<IActionResult> Lobbies()
    {
        return Json(await _repo.ToListPageViewModel());
    }

    [HttpPost]
    public async Task<IActionResult> UserGuess(string user, int lobbyId, string guessText)
    {
        if (user.Length > 10)
        {
            return BadRequest("错误，你的名字太长了！");
        }
        if (guessText.Length > 40)
        {
            return BadRequest("错误，你的猜测太长了！");
        }

        LobbyMessage message = await _repo.AddUserGuess(lobbyId, user, guessText);
        await _hubContext.Clients.Group($"lobby-{lobbyId}").OnNewMessage(message.ToViewModel());
        _ = CallLLM(lobbyId, user, guessText);
        return Ok();
    }

    private async Task CallLLM(int lobbyId, string user, string guessText)
    {
        using IServiceScope scope = scopeFactory.CreateScope();
        IConfiguration config = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        using Storage db = scope.ServiceProvider.GetRequiredService<Storage>();
        LobbyRepository repo = scope.ServiceProvider.GetRequiredService<LobbyRepository>();
        string prompt = @"你是猜成语App的用户助理，此次成语是：{{idiom}}({{charsCount}}个字)，该成语的解释是：{{explain}}
用于生成图片的revised prompt是：{{revisedPrompt}}
系统已根据该成语生成了一张图片发给用户，用户会猜成语是啥

你需要和用户对话，并适时的给予一些加油鼓励，但不要做任何提示
图片可能看不太出来，你可以调侃一下图片生成还不够先进
但如果用户明确需要，你可以根据用户需要，给少量提示，但不要太直白
禁止直接将这个成语直接告诉用户
如果用户请求，你可返回以🖼🖼🖼开头的消息，让系统监测并生成新图片发给用户，生成图片大约需要60秒

请用诙谐、精简的语言对话，如果猜对了，你需要夸奖用户
请不要回复任何不相关的话题
重要：如果用户答对了，回复时请以✅开头，它会用于系统检测猜成语游戏是否已经完成";
        IAIService llm = scope.ServiceProvider.GetRequiredService<IAIService>();

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
                ? (ChatMessage)ChatMessage.CreateAssistantMessage(x.Message)
                : ChatMessage.CreateUserMessage($"{x.User}: {x.Message}"))
            .ToArray();

        bool haveAction = false;
        await foreach (string full in llm.AskStream(
        [
            ChatMessage.CreateSystemMessage(systemPrompt), 
            ..historyPrompt
        ]).DeltaToFull())
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

        LobbyMessage message = await _repo.AddUserGuess(lobbyId, user, $"注意，我{user}触发了重新生成，请耐心等待60秒！");
        await _hubContext.Clients.Group($"lobby-{lobbyId}").OnNewMessage(message.ToViewModel());

        _ = GenerateImage(user, lobby, new WordExplain(lobby.Idiom), markError: true);

        return Ok();
    }
}
