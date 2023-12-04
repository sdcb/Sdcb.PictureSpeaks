using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Sdcb.PictureSpeaks.Hubs;
using Sdcb.PictureSpeaks.Services.DALL_E3;
using Sdcb.PictureSpeaks.Services.DB;
using System.Text.RegularExpressions;

namespace Sdcb.PictureSpeaks.Controllers;

public class MainController : Controller
{
    private readonly DallE3Client _dalle3;
    private readonly Storage _db;
    private readonly IHubContext<MainHub, IMainHubClient> _hubContext;

    public MainController(DallE3Client dalle3, Storage db, IHubContext<MainHub, IMainHubClient> hubContext)
    {
        _dalle3 = dalle3;
        _db = db;
        _hubContext = hubContext;
    }

    [Route("")]
    public IActionResult Index()
    {
        ViewData["Lobbies"] = _db.ToListPageViewModel();
        return View();
    }

    [HttpPost]
    public async Task<IActionResult> CreateLobby(string user, string idiom)
    {
        Lobby lobby = _db.Add(user, idiom);
        await _hubContext.Clients.All.RefreshLobby();
        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(500);
                ImageGeneratedResponse resp = await _dalle3.GenerateDallE3Image(new ImageGenerationOptions($"请为成语“{idiom}”生成一张符合意境的图片"));
                LobbyMessage message = lobby.AddImageMessage(resp.Data[0].Url);
                await _hubContext.Clients.Group($"lobby-{lobby.Id}").OnNewMessage(lobby.Id, message);
                await _hubContext.Clients.All.OnLobbyStatusChange(lobby.Id, LobbyStatus.Ready);
                Console.WriteLine($"{user}[{idiom}] --> {resp.Data[0].Url}");
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
                LobbyMessage message = lobby.AddErrorMessage(e.Message);
                await _hubContext.Clients.All.OnLobbyStatusChange(lobby.Id, LobbyStatus.Error);
                await _hubContext.Clients.Group($"lobby-{lobby.Id}").OnNewMessage(lobby.Id, message);
            }
        });
        
        return RedirectToAction("Index");
    }

    [HttpGet]
    public IActionResult Lobbies()
    {
        return Json(_db.ToListPageViewModel());
    }

    public IActionResult Lobby(int id)
    {
        ViewData["Lobby"] = _db.Lobbies[id];
        return View();
    }

    [HttpPost]
    public async Task<IActionResult> UserGuess(string user, int lobbyId, string guessText)
    {
        LobbyMessage message = _db.Lobbies[lobbyId].AddLobbyText(user, guessText);
        await _hubContext.Clients.Group($"lobby-{lobbyId}").OnNewMessage(lobbyId, message);
        return Json(message);
    }

    [HttpPost]
    public async Task<IActionResult> Generate(string prompt)
    {
        ImageGeneratedResponse resp = await _dalle3.GenerateDallE3Image(new ImageGenerationOptions(prompt));
        return Json(resp.Data[0].Url);
    }
}
