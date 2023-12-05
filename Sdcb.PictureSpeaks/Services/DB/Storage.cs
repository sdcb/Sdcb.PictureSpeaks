
using Microsoft.EntityFrameworkCore;
using Sdcb.PictureSpeaks.Hubs;

namespace Sdcb.PictureSpeaks.Services.DB;

public class Storage(DbContextOptions<Storage> options) : DbContext(options)
{
    public DbSet<Lobby> Lobby { get; init; } = null!;
    public DbSet<LobbyMessage> LobbyMessage { get; init; } = null!;
    public DbSet<LobbyHistoryMessage> LobbyHistoryMessage { get; init; } = null!;
    public DbSet<Dalle3Request> Dalle3Request { get; init; } = null!;
}

public class Dalle3Request
{
    public int Id { get; set; }

    public int LobbyId { get; set; }

    public Lobby Lobby { get; set; } = null!;

    public string AzureImageUrl { get; set; } = null!;

    public string RevisedPrompt { get; set; } = null!;

    public DateTime AzureGeneratedTime { get; set; }

    public string? LocalPath { get; set; }

    public DateTime? LocalDownloadedTime { get; set; }
}

public class Lobby
{
    public int Id { get; set; }

    public LobbyStatus LobbyStatus { get; set; }

    public string CreateUser { get; init; } = null!;

    public string Idiom { get; init; } = null!;

    public DateTime CreateTime { get; init; } = DateTime.Now;

    public virtual ICollection<Dalle3Request> Dalle3Requests { get; init; } = [];

    public virtual ICollection<LobbyMessage> Messages { get; init; } = [];

    public virtual ICollection<LobbyHistoryMessage> HistoryMessages { get; init; } = [];
}

public record LobbyHistoryMessage 
{
    public int Id { get; set; }
    public int OldMessageId { get; set; }
    public int LobbyId { get; set; }
    public Lobby Lobby { get; set; } = null!;
    public string User { get; init; } = null!;
    public string Message { get; set; } = null!;
    public DateTime DateTime { get; init; } = DateTime.Now;
    public MessageKind MessageKind { get; init; } = MessageKind.Text;
}

public record LobbyMessage
{
    public int Id { get; set; }
    public int LobbyId { get; set; }
    public Lobby Lobby { get; set; } = null!;
    public string User { get; init; } = null!;
    public string Message { get; set; } = null!;
    public DateTime DateTime { get; init; } = DateTime.Now;
    public MessageKind MessageKind { get; init; } = MessageKind.Text;

    public MessageViewModel ToViewModel() => new()
    {
        Id = Id,
        User = User,
        Message = Message,
        DateTime = DateTime,
        MessageKind = MessageKind,
    };

    public LobbyHistoryMessage ToHistoryMessage() => new()
    {
        OldMessageId = Id,
        LobbyId = LobbyId,
        Lobby = Lobby,
        User = User,
        Message = Message,
        DateTime = DateTime,
        MessageKind = MessageKind,
    };
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
    Completed,
}
