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

    public Task OnNewMessage(int lobbyId, LobbyMessage message);
    public Task OnLobbyStatusChange(int lobbyId, LobbyStatus status);
}