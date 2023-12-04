

using System;

namespace Sdcb.PictureSpeaks.Services.DB;

public class Storage
{
    public Dictionary<int, Lobby> Lobbies { get; init; } = new();

    public Lobby Add(string user, string idiom)
    {
        Lobby lobby = new()
        {
            Idiom = idiom,
            Creator = user,
            LobbyStatus = LobbyStatus.Pending,
            Users = new List<string>() { user },
        };
        Lobbies.Add(lobby.Id, lobby);
        return lobby;
    }
}

public class Lobby
{
    static IEnumerator<int> IdGenerator = Enumerable.Range(1, int.MaxValue).GetEnumerator();

    public Lobby()
    {
        IdGenerator.MoveNext();
        Id = IdGenerator.Current;
    }

    public int Id { get; }

    public LobbyStatus LobbyStatus { get; set; }

    public string Creator { get; init; } = null!;

    public string Idiom { get; init; } = null!;

    public List<string> Users { get; init; } = new();

    public List<LobbyMessage> Messages { get; init; } = new();

    public LobbyMessage AddImageMessage(string url)
    {
        LobbyMessage msg = new()
        {
            Message = url,
            User = "系统",
            MessageKind = MessageKind.Image
        };
        Messages.Add(msg);
        LobbyStatus = LobbyStatus.Ready;
        return msg;
    }

    public LobbyMessage AddErrorMessage(string message)
    {
        LobbyMessage msg = new()
        {
            Message = message,
            User = "系统",
            MessageKind = MessageKind.Error
        };
        Messages.Add(msg);
        LobbyStatus = LobbyStatus.Error;
        return msg;
    }
}

public class LobbyMessage
{
    public string User { get; init; } = null!;

    public string Message { get; init; } = null!;

    public MessageKind MessageKind { get; init; } = MessageKind.Text;
}

public enum MessageKind
{
    Text, Image, Error,
}

public enum LobbyStatus
{
    Pending,
    Ready,
    Error, 
}
