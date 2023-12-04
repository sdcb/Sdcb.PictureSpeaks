using Microsoft.AspNetCore.SignalR;
using Sdcb.PictureSpeaks.Services.DB;

namespace Sdcb.PictureSpeaks.Hubs;

public class MainHub : Hub<IMainHubClient>
{
    public void DoWork()
    {

    }
}

public interface IMainHubClient
{
    public Task RefreshLobby();

    public Task OnNewMessage(int lobbyId, LobbyMessage message);
}