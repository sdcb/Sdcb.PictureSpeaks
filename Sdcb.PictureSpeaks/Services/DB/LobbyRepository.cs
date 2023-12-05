using Azure.Core.Pipeline;
using Microsoft.EntityFrameworkCore;
using Sdcb.PictureSpeaks.Services.DALL_E3;

namespace Sdcb.PictureSpeaks.Services.DB;

public class LobbyRepository(Storage db, IServiceScopeFactory scope)
{
    private readonly Storage _db = db;
    private readonly IServiceScopeFactory _scopeFactory = scope;

    public async Task<Lobby> AddLobby(string user, string idiom)
    {
        Lobby lobby = new()
        {
            Idiom = idiom,
            CreateUser = user,
            LobbyStatus = LobbyStatus.Pending,
        };
        _db.Lobby.Add(lobby);
        await _db.SaveChangesAsync();
        return lobby;
    }

    internal async Task<object[]> ToListPageViewModel() => await _db.Lobby.Select(x => new
    {
        x.Id,
        x.LobbyStatus,
        x.CreateUser,
        x.Idiom,
    }).ToArrayAsync();

    public async Task<LobbyMessage> AddImageMessage(int lobbyId, Datum datum)
    {
        Lobby? lobby = await _db.Lobby.FindAsync(lobbyId) ?? throw new Exception("找不到房间");
        Dalle3Request req = new()
        {
            AzureImageUrl = datum.Url,
            RevisedPrompt = datum.RevisedPrompt,
            AzureGeneratedTime = DateTime.Now,
        };
        lobby.Dalle3Requests.Add(req);
        await _db.SaveChangesAsync();

        LobbyMessage msg = new()
        {
            Message = req.Id.ToString(),
            User = "系统",
            MessageKind = MessageKind.Image,
        };
        lobby.Messages.Add(msg);
        lobby.LobbyStatus = LobbyStatus.Ready;
        await _db.SaveChangesAsync();

        _ = DownloadToLocal(_scopeFactory, datum.Url, lobby, req);

        return msg;
    }

    private static async Task DownloadToLocal(IServiceScopeFactory scopeFactory, string url, Lobby lobby, Dalle3Request req)
    {
        using IServiceScope scope = scopeFactory.CreateScope();
        using Storage db = scope.ServiceProvider.GetRequiredService<Storage>();
        IWebHostEnvironment env = scope.ServiceProvider.GetRequiredService<IWebHostEnvironment>();

        db.Attach(req);

        HttpClient http = new();
        byte[] data = await http.GetByteArrayAsync(url);
        string dir = Path.Combine(env.WebRootPath, "images");
        Directory.CreateDirectory(dir);
        string fileName = $"{req.Id}-{lobby.Id}-{lobby.Idiom}.png";
        string path = Path.Combine(dir, fileName);
        await File.WriteAllBytesAsync(path, data);
        req.LocalDownloadedTime = DateTime.Now;
        req.LocalPath = fileName;
        
        await db.SaveChangesAsync();
    }

    public async Task<LobbyMessage> AddErrorMessage(int lobbyId, string message)
    {
        Lobby? lobby = await _db.Lobby.FindAsync(lobbyId) ?? throw new Exception("找不到房间");
        LobbyMessage msg = new()
        {
            Message = message,
            User = "系统",
            MessageKind = MessageKind.Error,
        };
        lobby.Messages.Add(msg);
        lobby.LobbyStatus = LobbyStatus.Error;
        await _db.SaveChangesAsync();
        return msg;
    }

    public async Task<LobbyMessage> AddUserGuess(int lobbyId, string user, string guessText)
    {
        Lobby? lobby = await _db.Lobby.FindAsync(lobbyId) ?? throw new Exception("找不到房间");
        LobbyMessage msg = new()
        {
            Message = guessText,
            User = user,
            MessageKind = MessageKind.Text,
        };
        lobby.Messages.Add(msg);
        await _db.SaveChangesAsync();
        return msg;
    }

    public async Task<LobbyMessage> AddAIResponse(int lobbyId, string responseText)
    {
        Lobby? lobby = await _db.Lobby.FindAsync(lobbyId) ?? throw new Exception("找不到房间");
        LobbyMessage msg = new()
        {
            Message = responseText,
            User = "AI",
            MessageKind = MessageKind.Text,
        };
        lobby.Messages.Add(msg);
        await _db.SaveChangesAsync();
        return msg;
    }

    public async Task<LobbyMessage> AddEmptyAIChatMessage(int lobbyId)
    {
        LobbyMessage msg = new()
        {
            LobbyId = lobbyId,
            User = "AI",
            MessageKind = MessageKind.Text,
            Message = "打字中...",
        };
        _db.LobbyMessage.Add(msg);
        await _db.SaveChangesAsync();
        return msg;
    }
}
