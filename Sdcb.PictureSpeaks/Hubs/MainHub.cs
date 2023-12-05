using Microsoft.AspNetCore.SignalR;
using Sdcb.PictureSpeaks.Services.DB;

namespace Sdcb.PictureSpeaks.Hubs;

public class MainHub : Hub<IMainHubClient>
{
    public async Task JoinLobby(int lobbyId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"lobby-{lobbyId}");
    }
}

public interface IMainHubClient
{
    public Task RefreshLobby();

    public Task OnNewMessage(MessageViewModel message);

    public Task OnMessageStreaming(int messageId, string content);

    public Task OnLobbyStatusChanged(int lobbyId, LobbyStatus status);
}

public class MessageViewModel
{
    public int Id { get; set; }
    public string User { get; init; } = null!;
    public string Message { get; set; } = null!;
    public DateTime DateTime { get; init; } = DateTime.Now;
    public MessageKind MessageKind { get; init; } = MessageKind.Text;
}